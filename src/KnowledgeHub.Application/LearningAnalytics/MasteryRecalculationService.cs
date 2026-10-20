using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>单个学生某课程的知识点掌握度快照。</summary>
public record KpMasterySnapshot(
    Guid KnowledgePointId,
    string Name,
    Guid? ChapterId,
    string? ChapterTitle,
    int SyllabusOrder,
    double Mastery,
    double Evidence);

/// <summary>
/// 掌握度重算：合并「在线作答（StudentExerciseRecord）」与「纸质试卷 / 课堂练习（AnswerRecord）」，
/// 按习题的知识点权重累计证据，Beta 平滑 + 时间衰减后写回 <see cref="StudentKpMastery"/>。
/// </summary>
public class MasteryRecalculationService : ITransientDependency
{
    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<ExerciseKnowledgePoint> _mapRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<StudentExerciseRecord> _onlineRecordRepository;
    private readonly IRepository<AnswerRecord, Guid> _answerRepository;
    private readonly IRepository<StudentKpMastery, Guid> _masteryRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly KnowledgePointIndexer _indexer;
    private readonly IGuidGenerator _guidGenerator;

    public MasteryRecalculationService(
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<ExerciseKnowledgePoint> mapRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<StudentExerciseRecord> onlineRecordRepository,
        IRepository<AnswerRecord, Guid> answerRepository,
        IRepository<StudentKpMastery, Guid> masteryRepository,
        IRepository<Chapter, Guid> chapterRepository,
        KnowledgePointIndexer indexer,
        IGuidGenerator guidGenerator)
    {
        _kpRepository = kpRepository;
        _mapRepository = mapRepository;
        _exerciseRepository = exerciseRepository;
        _onlineRecordRepository = onlineRecordRepository;
        _answerRepository = answerRepository;
        _masteryRepository = masteryRepository;
        _chapterRepository = chapterRepository;
        _indexer = indexer;
        _guidGenerator = guidGenerator;
    }

    /// <summary>课程分析上下文（一次加载，批量学生复用）。</summary>
    private record CourseContext(
        List<KnowledgePoint> KnowledgePoints,
        Dictionary<Guid, Exercise> Exercises,
        List<ExerciseKnowledgePoint> Maps,
        Dictionary<Guid, Chapter> Chapters);

    private async Task<CourseContext> LoadContextAsync(Guid courseId)
    {
        var kps = await _indexer.EnsureAsync(courseId);
        var exercises = await _exerciseRepository.GetListAsync(x => x.CourseId == courseId);
        var exerciseById = exercises.ToDictionary(e => e.Id);

        var maps = new List<ExerciseKnowledgePoint>();
        if (exerciseById.Any())
        {
            var exerciseIds = exerciseById.Keys.ToList();
            maps = await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId));
        }

        var chapters = await _chapterRepository.GetListAsync(x => x.CourseId == courseId);

        return new CourseContext(kps, exerciseById, maps, chapters.ToDictionary(c => c.Id));
    }

    /// <summary>重算并写回某学生某课程的知识点掌握度。</summary>
    public async Task<List<KpMasterySnapshot>> RecalculateAsync(Guid studentId, Guid courseId)
    {
        var context = await LoadContextAsync(courseId);
        var snapshots = await ComputeCoreAsync(studentId, context);
        await PersistAsync(studentId, snapshots);
        return snapshots;
    }

    /// <summary>计算（不写库）。</summary>
    public async Task<List<KpMasterySnapshot>> ComputeAsync(Guid studentId, Guid courseId)
    {
        var context = await LoadContextAsync(courseId);
        return await ComputeCoreAsync(studentId, context);
    }

    /// <summary>批量计算（不写库）。</summary>
    public async Task<Dictionary<Guid, List<KpMasterySnapshot>>> ComputeForStudentsAsync(
        IReadOnlyCollection<Guid> studentIds,
        Guid courseId)
    {
        var context = await LoadContextAsync(courseId);
        var result = new Dictionary<Guid, List<KpMasterySnapshot>>();
        foreach (var studentId in studentIds.Distinct())
        {
            result[studentId] = await ComputeCoreAsync(studentId, context);
        }

        return result;
    }

    private async Task<List<KpMasterySnapshot>> ComputeCoreAsync(Guid studentId, CourseContext context)
    {
        if (!context.KnowledgePoints.Any() || !context.Maps.Any())
        {
            return BuildSnapshots(context.KnowledgePoints, new(), context.Chapters);
        }

        var observations = new Dictionary<Guid, List<MasteryCalculator.Observation>>();

        void Add(Guid kpId, double kpWeight, double ratio, double fullScore, DateTime at)
        {
            if (!observations.TryGetValue(kpId, out var list))
            {
                list = new List<MasteryCalculator.Observation>();
                observations[kpId] = list;
            }

            list.Add(new MasteryCalculator.Observation(
                Math.Clamp(ratio, 0, 1),
                MasteryCalculator.WeightFromFullScore(kpWeight, fullScore),
                at));
        }

        var exerciseIds = context.Exercises.Keys.ToList();

        // 在线作答
        var online = await _onlineRecordRepository.GetListAsync(x =>
            x.StudentId == studentId && exerciseIds.Contains(x.ExerciseId) && x.IsCorrect != null);
        foreach (var record in online)
        {
            if (!context.Exercises.TryGetValue(record.ExerciseId, out var exercise))
            {
                continue;
            }

            var ratio = record.IsCorrect == true ? 1 : 0;
            var fullScore = exercise.Score > 0 ? exercise.Score : 1;
            var at = record.CompletedAt ?? record.CreationTime;
            foreach (var map in context.Maps.Where(m => m.ExerciseId == record.ExerciseId))
            {
                Add(map.KnowledgePointId, map.Weight, ratio, fullScore, at);
            }
        }

        // 纸质试卷 / 课堂练习
        var answers = await _answerRepository.GetListAsync(x =>
            x.StudentId == studentId && exerciseIds.Contains(x.ExerciseId));
        foreach (var answer in answers)
        {
            foreach (var map in context.Maps.Where(m => m.ExerciseId == answer.ExerciseId))
            {
                Add(map.KnowledgePointId, map.Weight, answer.Ratio,
                    answer.FullScore > 0 ? answer.FullScore : 1, answer.AnsweredAt);
            }
        }

        return BuildSnapshots(context.KnowledgePoints, observations, context.Chapters);
    }

    private async Task PersistAsync(Guid studentId, List<KpMasterySnapshot> snapshots)
    {
        if (!snapshots.Any())
        {
            return;
        }

        var kpIds = snapshots.Select(s => s.KnowledgePointId).ToList();
        var existing = await _masteryRepository.GetListAsync(x =>
            x.StudentId == studentId && kpIds.Contains(x.KnowledgePointId));
        var byKp = existing.ToDictionary(x => x.KnowledgePointId);
        var now = DateTime.UtcNow;

        foreach (var snapshot in snapshots)
        {
            if (byKp.TryGetValue(snapshot.KnowledgePointId, out var row))
            {
                row.Mastery = snapshot.Mastery;
                row.Evidence = snapshot.Evidence;
                row.UpdatedAt = now;
                await _masteryRepository.UpdateAsync(row, autoSave: true);
            }
            else
            {
                var created = new StudentKpMastery(_guidGenerator.Create(), studentId, snapshot.KnowledgePointId)
                {
                    Mastery = snapshot.Mastery,
                    Evidence = snapshot.Evidence,
                    UpdatedAt = now,
                };
                await _masteryRepository.InsertAsync(created, autoSave: true);
            }
        }
    }

    private static List<KpMasterySnapshot> BuildSnapshots(
        List<KnowledgePoint> kps,
        Dictionary<Guid, List<MasteryCalculator.Observation>> observations,
        Dictionary<Guid, Chapter> chapters)
    {
        var now = DateTime.UtcNow;
        var result = new List<KpMasterySnapshot>();

        foreach (var kp in kps.Where(k => k.CanonicalId == null))
        {
            string? chapterTitle = null;
            if (kp.ChapterId.HasValue && chapters.TryGetValue(kp.ChapterId.Value, out var chapter))
            {
                chapterTitle = chapter.Title;
            }

            if (observations.TryGetValue(kp.Id, out var list) && list.Any())
            {
                var (mastery, evidence) = MasteryCalculator.Compute(list, now);
                result.Add(new KpMasterySnapshot(kp.Id, kp.Name, kp.ChapterId, chapterTitle, kp.SyllabusOrder, mastery, evidence));
            }
            else
            {
                // 无作答证据：保留条目但 Evidence=0，前端按「数据不足」处理，不判为薄弱
                result.Add(new KpMasterySnapshot(kp.Id, kp.Name, kp.ChapterId, chapterTitle, kp.SyllabusOrder, 0, 0));
            }
        }

        return result.OrderBy(s => s.SyllabusOrder).ThenBy(s => s.Name).ToList();
    }
}
