using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Exams;
using KnowledgeHub.LearningAnalytics;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowledgeHub.Learning;

/// <summary>单个课程知识点的掌握度快照。</summary>
public record KnowledgeResourceMasterySnapshot(
    Guid KnowledgeResourceId,
    string Name,
    KnowledgeNodeType NodeType,
    Guid? ParentId,
    double Mastery,
    double Evidence,
    double WeakLine,
    double OkLine,
    int PracticeCount = 0,
    int CorrectCount = 0,
    DateTime? LastPracticeAt = null);

/// <summary>
/// 课程知识点掌握度重算（课程侧唯一算法）。
/// <para>
/// 与「智学分析」的 <see cref="MasteryRecalculationService"/> 共用同一个
/// <see cref="MasteryCalculator"/>（Beta 先验 + 120 天半衰期），区别只在证据来源与归属粒度：
/// </para>
/// <list type="bullet">
/// <item>智学分析侧：知识点 = <c>LearningAnalytics.KnowledgePoint</c>，按「习题 × 知识点权重」摊分（纸质试卷场景）。</item>
/// <item>课程侧（本服务）：知识点 = <c>Course.KnowledgeResource</c>，按 <c>Exercise.KnowledgeResourceId</c> 归属（在线学习场景）。</item>
/// </list>
/// <para>
/// 单元节点（<see cref="KnowledgeNodeType.Unit"/>）没有直接作答证据时，
/// 用子孙节点的证据加权平均做「汇总值」，方便前端在树上直接显示进度。
/// </para>
/// </summary>
public class CourseMasteryRecalculationService : ITransientDependency
{
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<StudentExerciseRecord, Guid> _recordRepository;
    private readonly IRepository<StudentAnswer, Guid> _answerRepository;
    private readonly IRepository<StudentExam, Guid> _studentExamRepository;
    private readonly IRepository<KnowledgeMastery, Guid> _masteryRepository;
    private readonly ThresholdResolver _thresholdResolver;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IDataFilter _dataFilter;

    public CourseMasteryRecalculationService(
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<StudentExerciseRecord, Guid> recordRepository,
        IRepository<StudentAnswer, Guid> answerRepository,
        IRepository<StudentExam, Guid> studentExamRepository,
        IRepository<KnowledgeMastery, Guid> masteryRepository,
        ThresholdResolver thresholdResolver,
        IGuidGenerator guidGenerator,
        IDataFilter dataFilter)
    {
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _exerciseRepository = exerciseRepository;
        _recordRepository = recordRepository;
        _answerRepository = answerRepository;
        _studentExamRepository = studentExamRepository;
        _masteryRepository = masteryRepository;
        _thresholdResolver = thresholdResolver;
        _guidGenerator = guidGenerator;
        _dataFilter = dataFilter;
    }

    /// <summary>重算并写回：某学生某课程的全部知识点掌握度。</summary>
    public async Task<List<KnowledgeResourceMasterySnapshot>> RecalculateAsync(Guid studentId, Guid courseId)
    {
        var snapshots = await ComputeAsync(studentId, courseId);
        await PersistAsync(studentId, snapshots);
        return snapshots;
    }

    /// <summary>只计算不写库（教师端预览 / 诊断用）。</summary>
    public async Task<List<KnowledgeResourceMasterySnapshot>> ComputeAsync(Guid studentId, Guid courseId)
    {
        // 统一关掉租户过滤器再算：
        // ① 知识点树（KnowledgeResource）本身不带租户，但习题 / 作答记录是 IMultiTenant；
        // ② 共建库共享课程时，课程的习题属于主建校、学生的作答属于其本校，带过滤器会两边都查不到；
        // ③ 宿主管理员（CurrentTenant=null）打开任一门课也要能算。
        // 安全性由「courseId + studentId + 习题属于本课程」三重约束保证，不会串学生。
        using var tenantFilter = _dataFilter.Disable<IMultiTenant>();

        var nodes = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == courseId);
        if (nodes.Count == 0)
        {
            return new List<KnowledgeResourceMasterySnapshot>();
        }

        // 习题 → 知识点归属
        var exercises = await _exerciseRepository.GetListAsync(x =>
            x.CourseId == courseId && x.KnowledgeResourceId != null);
        var exerciseById = exercises.ToDictionary(x => x.Id);
        var exerciseIds = exerciseById.Keys.ToList();

        var observations = new Dictionary<Guid, List<MasteryCalculator.Observation>>();
        var stats = new Dictionary<Guid, (int Practice, int Correct, DateTime? Last)>();

        void Add(Guid nodeId, double ratio, double fullScore, DateTime at, bool isCorrect)
        {
            if (!observations.TryGetValue(nodeId, out var list))
            {
                list = new List<MasteryCalculator.Observation>();
                observations[nodeId] = list;
            }

            list.Add(new MasteryCalculator.Observation(
                Math.Clamp(ratio, 0, 1),
                MasteryCalculator.WeightFromFullScore(1, fullScore),
                at));

            var current = stats.TryGetValue(nodeId, out var s) ? s : (Practice: 0, Correct: 0, Last: (DateTime?)null);
            stats[nodeId] = (
                current.Practice + 1,
                current.Correct + (isCorrect ? 1 : 0),
                !current.Last.HasValue || at > current.Last.Value ? at : current.Last.Value);
        }

        // 证据 1：在线习题作答
        if (exerciseIds.Count > 0)
        {
            var records = await _recordRepository.GetListAsync(x =>
                x.StudentId == studentId && exerciseIds.Contains(x.ExerciseId) && x.IsCorrect != null);

            foreach (var record in records)
            {
                if (!exerciseById.TryGetValue(record.ExerciseId, out var exercise) ||
                    !exercise.KnowledgeResourceId.HasValue)
                {
                    continue;
                }

                var isCorrect = record.IsCorrect == true;
                Add(
                    exercise.KnowledgeResourceId.Value,
                    isCorrect ? 1 : 0,
                    exercise.Score > 0 ? exercise.Score : 1,
                    record.CompletedAt ?? record.CreationTime,
                    isCorrect);
            }

            // 证据 2：在线考试作答（StudentAnswer，需先定位该学生的考试记录）
            var studentExams = await _studentExamRepository.GetListAsync(x => x.StudentId == studentId);
            if (studentExams.Count > 0)
            {
                var studentExamById = studentExams.ToDictionary(x => x.Id);
                var studentExamIds = studentExamById.Keys.ToList();
                var answers = await _answerRepository.GetListAsync(x =>
                    exerciseIds.Contains(x.ExerciseId) &&
                    studentExamIds.Contains(x.StudentExamId));

                foreach (var answer in answers)
                {
                    if (!exerciseById.TryGetValue(answer.ExerciseId, out var exercise) ||
                        !exercise.KnowledgeResourceId.HasValue)
                    {
                        continue;
                    }

                    var ratio = answer.IsCorrect.HasValue
                        ? (answer.IsCorrect.Value ? 1d : 0d)
                        : exercise.Score > 0 && answer.Score.HasValue
                            ? (double)answer.Score.Value / exercise.Score
                            : 0d;

                    // StudentAnswer 没有时间戳：优先用交卷时间，退化为当前时间（半衰期衰减按天算，误差可接受）
                    var at = studentExamById.TryGetValue(answer.StudentExamId, out var se)
                             && se.SubmittedAt.HasValue
                        ? se.SubmittedAt!.Value
                        : DateTime.UtcNow;

                    Add(
                        exercise.KnowledgeResourceId.Value,
                        ratio,
                        exercise.Score > 0 ? exercise.Score : 1,
                        at,
                        answer.IsCorrect == true);
                }
            }
        }

        var thresholds = await _thresholdResolver.ResolveAsync(courseId);
        var now = DateTime.UtcNow;

        // 叶子节点的直接掌握度
        var direct = new Dictionary<Guid, (double Mastery, double Evidence)>();
        foreach (var node in nodes.Where(n => observations.ContainsKey(n.Id)))
        {
            direct[node.Id] = MasteryCalculator.Compute(observations[node.Id], now);
        }

        // 汇总：单元节点无直接证据时，用子孙证据加权平均
        var childrenByParent = nodes
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        (double Mastery, double Evidence) Rollup(Guid nodeId, int depth)
        {
            if (direct.TryGetValue(nodeId, out var own))
            {
                return own;
            }

            if (depth > 32 || !childrenByParent.TryGetValue(nodeId, out var children))
            {
                return (0, 0);
            }

            double weighted = 0, weight = 0;
            foreach (var child in children)
            {
                var (m, e) = Rollup(child.Id, depth + 1);
                if (e <= 0)
                {
                    continue;
                }

                weighted += m * e;
                weight += e;
            }

            return weight > 0 ? (weighted / weight, weight) : (0, 0);
        }

        var result = new List<KnowledgeResourceMasterySnapshot>(nodes.Count);
        foreach (var node in nodes)
        {
            var (mastery, evidence) = Rollup(node.Id, 0);
            var nodeStats = stats.TryGetValue(node.Id, out var s) ? s : (Practice: 0, Correct: 0, Last: (DateTime?)null);
            result.Add(new KnowledgeResourceMasterySnapshot(
                node.Id,
                node.Name,
                node.NodeType,
                node.ParentId,
                mastery,
                evidence,
                thresholds.Weak,
                thresholds.Ok,
                nodeStats.Practice,
                nodeStats.Correct,
                nodeStats.Last));
        }

        return result;
    }

    private async Task PersistAsync(Guid studentId, List<KnowledgeResourceMasterySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var nodeIds = snapshots.Select(x => x.KnowledgeResourceId).ToList();
        var existing = await _masteryRepository.GetListAsync(x =>
            x.StudentId == studentId && nodeIds.Contains(x.KnowledgeResourceId));
        var byNode = existing.ToDictionary(x => x.KnowledgeResourceId);
        var now = DateTime.UtcNow;

        foreach (var snapshot in snapshots)
        {
            if (byNode.TryGetValue(snapshot.KnowledgeResourceId, out var row))
            {
                row.SetMastery(snapshot.Mastery, snapshot.Evidence, snapshot.OkLine, snapshot.WeakLine);
                ApplyCounters(row, snapshot, now);
                await _masteryRepository.UpdateAsync(row, autoSave: true);
            }
            else
            {
                var created = new KnowledgeMastery(
                    _guidGenerator.Create(),
                    studentId,
                    snapshot.KnowledgeResourceId);
                created.SetMastery(snapshot.Mastery, snapshot.Evidence, snapshot.OkLine, snapshot.WeakLine);
                ApplyCounters(created, snapshot, now);
                await _masteryRepository.InsertAsync(created, autoSave: true);
            }
        }
    }

    /// <summary>同步展示口径的练习次数 / 正确次数 / 最近练习时间。</summary>
    private static void ApplyCounters(KnowledgeMastery row, KnowledgeResourceMasterySnapshot snapshot, DateTime now)
    {
        row.PracticeCount = snapshot.PracticeCount;
        row.CorrectCount = snapshot.CorrectCount;
        row.LastPracticeAt = snapshot.LastPracticeAt ?? row.LastPracticeAt;
        row.UpdatedAt = now;
    }
}
