using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.LearningAnalytics.Dtos;
using KnowledgeHub.LearningAnalytics.Enums;
using KnowledgeHub.LearningAnalytics.Ml;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 推荐引擎（阶段 B 版本）：
/// - 专项练习：内容相似（薄弱向量 × 习题知识点）+ 难度适配（预测答对概率趋近 0.65）
///   + 「最近做过的题」降权去重；协同过滤接口预留（当前无邻居数据即退化为纯内容推荐）。
/// - 衔接复习包：沿 Confirmed 先修边向前追溯，取薄弱点，按影响面 / 掌握度排序。
/// </summary>
public class RecommendationEngine : ITransientDependency
{
    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<ExerciseKnowledgePoint> _mapRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<KpPrerequisite, Guid> _prerequisiteRepository;
    private readonly IRepository<CourseLink, Guid> _courseLinkRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentExerciseRecord> _onlineRepository;
    private readonly IRepository<AnswerRecord, Guid> _answerRepository;
    private readonly IRepository<ReviewPackage, Guid> _packageRepository;
    private readonly IRepository<ReviewPackageItem, Guid> _packageItemRepository;
    private readonly MasteryRecalculationService _masteryService;
    private readonly KnowledgePointIndexer _indexer;
    private readonly ThresholdResolver _thresholdResolver;
    private readonly MasteryPredictorHolder _predictorHolder;
    private readonly RecommendationWeightProvider _weightProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    public RecommendationEngine(
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<ExerciseKnowledgePoint> mapRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<KpPrerequisite, Guid> prerequisiteRepository,
        IRepository<CourseLink, Guid> courseLinkRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentExerciseRecord> onlineRepository,
        IRepository<AnswerRecord, Guid> answerRepository,
        IRepository<ReviewPackage, Guid> packageRepository,
        IRepository<ReviewPackageItem, Guid> packageItemRepository,
        MasteryRecalculationService masteryService,
        KnowledgePointIndexer indexer,
        ThresholdResolver thresholdResolver,
        MasteryPredictorHolder predictorHolder,
        RecommendationWeightProvider weightProvider,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator)
    {
        _kpRepository = kpRepository;
        _mapRepository = mapRepository;
        _exerciseRepository = exerciseRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _courseLinkRepository = courseLinkRepository;
        _courseRepository = courseRepository;
        _onlineRepository = onlineRepository;
        _answerRepository = answerRepository;
        _packageRepository = packageRepository;
        _packageItemRepository = packageItemRepository;
        _masteryService = masteryService;
        _indexer = indexer;
        _thresholdResolver = thresholdResolver;
        _predictorHolder = predictorHolder;
        _weightProvider = weightProvider;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
    }

    // ══════════════════════════════════════════════════════
    //  专项练习推荐
    // ══════════════════════════════════════════════════════

    public async Task<List<PracticeItemDto>> RecommendPracticeAsync(
        Guid studentId,
        Guid courseId,
        IReadOnlyList<KpMasterySnapshot>? snapshots = null,
        int maxKnowledgePoints = 3)
    {
        snapshots ??= await _masteryService.ComputeAsync(studentId, courseId);
        var thresholds = await _thresholdResolver.ResolveAllAsync(courseId);
        var course = await _courseRepository.FindAsync(courseId);

        var predictor = _predictorHolder.Get(_currentTenant.Id);
        var weak = snapshots
            .Where(s => s.Evidence >= 1 && thresholds.Judge(s.KnowledgePointId, s.Mastery) != MasteryGrade.Ok)
            .OrderBy(s => s.Mastery)
            .Take(maxKnowledgePoints)
            .ToList();

        // 冷启动补弱：学生完全没做过的知识点，用矩阵分解/基线预测值挑 1 个最可能薄弱的
        if (predictor.IsReady)
        {
            var predictedWeak = snapshots
                .Where(s => s.Evidence <= 0)
                .Select(s => (Snapshot: s, Predicted: predictor.Predict(studentId, s.KnowledgePointId)))
                .Where(x => thresholds.Judge(x.Snapshot.KnowledgePointId, x.Predicted) == MasteryGrade.Weak)
                .OrderBy(x => x.Predicted)
                .Take(weak.Any() ? 1 : 2)
                .Select(x => x.Snapshot)
                .ToList();

            weak.AddRange(predictedWeak);
            weak = weak.DistinctBy(s => s.KnowledgePointId).ToList();
        }

        if (!weak.Any())
        {
            return new List<PracticeItemDto>();
        }

        var weakIds = weak.Select(w => w.KnowledgePointId).ToList();
        var maps = await _mapRepository.GetListAsync(m => weakIds.Contains(m.KnowledgePointId));
        if (!maps.Any())
        {
            return new List<PracticeItemDto>();
        }

        var exerciseIds = maps.Select(m => m.ExerciseId).Distinct().ToList();
        var exercises = await _exerciseRepository.GetListAsync(x => exerciseIds.Contains(x.Id));
        var exerciseById = exercises.ToDictionary(e => e.Id);

        var recentlyCorrect = await LoadRecentlyCorrectExerciseIdsAsync(studentId, exerciseIds);

        // 协同信号：相似学生在候选题上的错误率（无邻居数据时为空 → 退化为纯内容 + 难度）
        var neighbors = predictor.Neighbors(studentId, 30);
        var collaborative = await LoadNeighborErrorRatesAsync(exerciseIds, neighbors);

        // 三路权重：Setting（可一键应用）→ appsettings，便于用离线评估结果定标
        var weights = await _weightProvider.GetAsync();

        var result = new List<PracticeItemDto>();
        foreach (var kp in weak)
        {
            var isPredicted = kp.Evidence <= 0;
            var effectiveMastery = isPredicted ? predictor.Predict(studentId, kp.KnowledgePointId) : kp.Mastery;

            var kpExerciseIds = maps.Where(m => m.KnowledgePointId == kp.KnowledgePointId)
                .Select(m => m.ExerciseId)
                .Distinct()
                .Where(id => exerciseById.ContainsKey(id) && !recentlyCorrect.Contains(id))
                .ToList();

            if (!kpExerciseIds.Any())
            {
                continue;
            }

            var ranked = kpExerciseIds
                .Select(id => exerciseById[id])
                .Select(e =>
                {
                    var content = Math.Clamp(1 - effectiveMastery, 0, 1);
                    var collab = collaborative.GetValueOrDefault(e.Id, 0);
                    var fit = RecommendationScorer.FitScore(effectiveMastery, e.Difficulty);
                    return new
                    {
                        Exercise = e,
                        Score = RecommendationScorer.Score(weights, content, collab, fit),
                    };
                })
                .OrderByDescending(x => x.Score)
                .Take(6)
                .Select(x => x.Exercise)
                .ToList();

            if (!ranked.Any())
            {
                continue;
            }

            var percent = (int)Math.Round(effectiveMastery * 100);
            var kpTarget = thresholds.For(kp.KnowledgePointId).Ok;
            var reason = isPredicted
                ? $"{kp.Name} 暂无作答数据，模型预测掌握率约 {percent}%，建议先做基础巩固"
                : $"{kp.Name} 掌握率 {percent}%（低于目标 {(int)(kpTarget * 100)}%）";

            result.Add(new PracticeItemDto
            {
                Id = $"kp-{kp.KnowledgePointId:N}",
                Title = $"{kp.Name}专项练习",
                CourseId = courseId,
                CourseTitle = course?.Title ?? string.Empty,
                ExerciseCount = ranked.Count,
                KnowledgePointId = kp.KnowledgePointId,
                FromPrerequisite = false,
                Reason = reason,
                ExerciseIds = ranked.Select(e => e.Id).ToList(),
            });
        }

        return result;
    }

    /// <summary>相似学生在候选题上的加权错误率（协同过滤信号，0=都不错，1=都错）。</summary>
    private async Task<Dictionary<Guid, double>> LoadNeighborErrorRatesAsync(
        List<Guid> exerciseIds,
        IReadOnlyList<(Guid StudentId, double Similarity)> neighbors)
    {
        var result = new Dictionary<Guid, double>();
        if (!exerciseIds.Any() || neighbors.Count == 0)
        {
            return result;
        }

        var neighborIds = neighbors.Select(n => n.StudentId).ToList();
        var similarity = neighbors.ToDictionary(n => n.StudentId, n => n.Similarity);

        var sums = new Dictionary<Guid, (double Weighted, double Weights)>();

        void Accumulate(Guid studentId, Guid exerciseId, double ratio)
        {
            var weight = similarity.GetValueOrDefault(studentId, 0);
            if (weight <= 0) return;

            var current = sums.GetValueOrDefault(exerciseId, (0, 0));
            sums[exerciseId] = (current.Weighted + weight * (1 - ratio), current.Weights + weight);
        }

        var answers = await _answerRepository.GetListAsync(a =>
            neighborIds.Contains(a.StudentId) && exerciseIds.Contains(a.ExerciseId));
        foreach (var answer in answers)
        {
            Accumulate(answer.StudentId, answer.ExerciseId, answer.Ratio);
        }

        var online = await _onlineRepository.GetListAsync(r =>
            neighborIds.Contains(r.StudentId) && exerciseIds.Contains(r.ExerciseId) && r.IsCorrect != null);
        foreach (var record in online)
        {
            Accumulate(record.StudentId, record.ExerciseId, record.IsCorrect == true ? 1 : 0);
        }

        foreach (var (exerciseId, value) in sums.Where(s => s.Value.Weights > 0))
        {
            result[exerciseId] = Math.Clamp(value.Weighted / value.Weights, 0, 1);
        }

        return result;
    }

    private async Task<HashSet<Guid>> LoadRecentlyCorrectExerciseIdsAsync(Guid studentId, List<Guid> exerciseIds)
    {
        var set = new HashSet<Guid>();

        var online = await _onlineRepository.GetListAsync(x =>
            x.StudentId == studentId && exerciseIds.Contains(x.ExerciseId) && x.IsCorrect == true);
        foreach (var record in online)
        {
            set.Add(record.ExerciseId);
        }

        var answers = await _answerRepository.GetListAsync(x =>
            x.StudentId == studentId && exerciseIds.Contains(x.ExerciseId));
        foreach (var answer in answers.Where(a => a.Ratio >= 0.9))
        {
            set.Add(answer.ExerciseId);
        }

        return set;
    }

    // ══════════════════════════════════════════════════════
    //  衔接复习包（课程联动）
    // ══════════════════════════════════════════════════════

    public async Task<ReviewPackageProgressDto?> BuildReviewPackageAsync(
        Guid studentId,
        Guid upcomingCourseId,
        PathStrategy strategy = PathStrategy.Mixed,
        bool persist = true)
    {
        var upcomingKps = await _indexer.EnsureAsync(upcomingCourseId);
        if (!upcomingKps.Any())
        {
            return null;
        }

        var course = await _courseRepository.FindAsync(upcomingCourseId);
        var upcomingKpIds = upcomingKps.Select(k => k.Id).ToList();

        // 1) Confirmed 先修边：后续知识点 -> 前置知识点
        var edges = await _prerequisiteRepository.GetListAsync(x =>
            x.Status == PrerequisiteStatus.Confirmed && upcomingKpIds.Contains(x.ToKpId));

        var impacts = edges
            .GroupBy(e => e.FromKpId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ToKpId).Distinct().Count());
        var candidateIds = edges.Select(e => e.FromKpId).Distinct().ToHashSet();

        // 2) 没有配置先修边时，退化为「前置课程的全部薄弱知识点」
        if (!candidateIds.Any())
        {
            var links = await _courseLinkRepository.GetListAsync(x => x.SuccessorCourseId == upcomingCourseId);
            foreach (var link in links)
            {
                var predecessorKps = await _indexer.EnsureAsync(link.PredecessorCourseId);
                foreach (var kp in predecessorKps)
                {
                    candidateIds.Add(kp.Id);
                }
            }
        }

        if (!candidateIds.Any())
        {
            return null;
        }

        var candidateKps = await _kpRepository.GetListAsync(x => candidateIds.Contains(x.Id));
        if (!candidateKps.Any())
        {
            return null;
        }

        // 3) 逐个候选知识点取掌握度（按所属课程缓存计算）
        var masteryByCourse = new Dictionary<Guid, Dictionary<Guid, KpMasterySnapshot>>();
        var items = new List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)>();
        // 前置课程里「完全没有作答证据」的知识点：不直接当作薄弱点，仅在无实测数据时用模型预测兜底
        var noEvidence = new List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)>();

        foreach (var group in candidateKps.GroupBy(k => k.CourseId))
        {
            if (!masteryByCourse.TryGetValue(group.Key, out var map))
            {
                var snaps = await _masteryService.ComputeAsync(studentId, group.Key);
                map = snaps.ToDictionary(s => s.KnowledgePointId);
                masteryByCourse[group.Key] = map;
            }

            var thresholds = await _thresholdResolver.ResolveAsync(group.Key);
            foreach (var kp in group)
            {
                if (!map.TryGetValue(kp.Id, out var snapshot))
                {
                    continue;
                }

                var impact = impacts.GetValueOrDefault(kp.Id, 1);

                if (snapshot.Evidence <= 0)
                {
                    noEvidence.Add((kp, snapshot, MasteryGrade.Weak, impact));
                    continue;
                }

                var grade = thresholds.Judge(snapshot.Mastery);
                if (grade == MasteryGrade.Ok)
                {
                    continue;
                }

                items.Add((kp, snapshot, grade, impact));
            }
        }

        if (!items.Any())
        {
            // 学生在前置课程里没有任何作答记录：用矩阵分解/基线预测挑出最可能薄弱的几个知识点，
            // 保证「衔接复习包」不会空（也不至于把整门课都塞进来）。
            var predictor = _predictorHolder.Get(_currentTenant.Id);
            var fallbackLimit = 5;

            items = noEvidence
                .Select(x => (
                    x.Kp,
                    Snapshot: predictor.IsReady
                        ? x.Snapshot with { Mastery = predictor.Predict(studentId, x.Kp.Id) }
                        : x.Snapshot,
                    Grade: predictor.IsReady
                        ? MasteryGrade.Weak
                        : x.Grade,
                    x.Impact))
                .OrderBy(x => x.Snapshot.Mastery)
                .Take(fallbackLimit)
                .ToList();
        }

        if (!items.Any())
        {
            return null;
        }

        // 4) 排序
        var ordered = strategy switch
        {
            PathStrategy.WeakFirst => items.OrderBy(i => i.Snapshot.Mastery).ToList(),
            PathStrategy.Syllabus => items
                .OrderBy(i => i.Kp.CourseId)
                .ThenBy(i => i.Snapshot.SyllabusOrder)
                .ToList(),
            _ => TopologicalOrder(items, edges),
        };

        // 5) 落库（同一学生 + 课程只保留一份）
        var package = await UpsertPackageAsync(studentId, upcomingCourseId, course?.Title ?? string.Empty, strategy, ordered, persist);
        if (package == null)
        {
            return null;
        }

        return await BuildProgressDtoAsync(package, ordered);
    }

    private async Task<ReviewPackageProgressDto> BuildProgressDtoAsync(
        ReviewPackage package,
        List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)> ordered)
    {
        var courseIds = ordered.Select(i => i.Kp.CourseId).Distinct().ToList();
        var courses = await _courseRepository.GetListAsync(c => courseIds.Contains(c.Id));
        var courseTitles = courses.ToDictionary(c => c.Id, c => c.Title);

        var items = await _packageItemRepository.GetListAsync(x => x.PackageId == package.Id);
        var itemByKp = items.ToDictionary(x => x.KnowledgePointId);

        var dto = new ReviewPackageProgressDto
        {
            ReviewPackageId = package.Id,
            UpcomingCourseId = package.UpcomingCourseId,
            UpcomingCourseTitle = package.UpcomingCourseTitle,
            Total = items.Count,
            Completed = items.Count(x => x.IsCompleted),
        };

        foreach (var entry in ordered)
        {
            var isCompleted = itemByKp.TryGetValue(entry.Kp.Id, out var row) && row.IsCompleted;
            dto.Items.Add(new ReviewPackageItemDto
            {
                KnowledgePointId = entry.Kp.Id,
                Name = entry.Kp.Name,
                CourseId = entry.Kp.CourseId,
                CourseTitle = courseTitles.GetValueOrDefault(entry.Kp.CourseId, string.Empty),
                Mastery = entry.Snapshot.Mastery,
                Level = (int)entry.Grade,
                IsCompleted = isCompleted,
            });
        }

        return dto;
    }

    private async Task<ReviewPackage?> UpsertPackageAsync(
        Guid studentId,
        Guid upcomingCourseId,
        string upcomingCourseTitle,
        PathStrategy strategy,
        List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)> ordered,
        bool persist)
    {
        if (!persist)
        {
            return new ReviewPackage(Guid.NewGuid(), studentId, upcomingCourseId)
            {
                UpcomingCourseTitle = upcomingCourseTitle,
                Strategy = strategy,
            };
        }

        var existing = await _packageRepository.GetListAsync(x =>
            x.StudentId == studentId && x.UpcomingCourseId == upcomingCourseId);
        var package = existing.OrderByDescending(x => x.GeneratedAt).FirstOrDefault();

        if (package == null)
        {
            package = new ReviewPackage(_guidGenerator.Create(), studentId, upcomingCourseId);
            package.UpcomingCourseTitle = upcomingCourseTitle;
            package.Strategy = strategy;
            await _packageRepository.InsertAsync(package, autoSave: true);
        }
        else
        {
            package.UpcomingCourseTitle = upcomingCourseTitle;
            package.Strategy = strategy;
            package.GeneratedAt = DateTime.UtcNow;
            await _packageItemRepository.DeleteAsync(x => x.PackageId == package.Id);
            await _packageRepository.UpdateAsync(package, autoSave: true);
        }

        var order = 0;
        foreach (var entry in ordered)
        {
            order++;
            await _packageItemRepository.InsertAsync(new ReviewPackageItem(
                _guidGenerator.Create(), package.Id, entry.Kp.Id)
            {
                SortOrder = order,
                Mastery = entry.Snapshot.Mastery,
                Impacts = entry.Impact,
            }, autoSave: true);
        }

        return package;
    }

    /// <summary>
    /// 混合策略：先修在前；同层按影响面大、掌握度低优先（拓扑排序）。
    /// 存在环（脏数据）时，剩余节点按掌握度追加，避免丢项。
    /// </summary>
    private static List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)>
        TopologicalOrder(
            List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)> items,
            List<KpPrerequisite> edges)
    {
        var byId = items.ToDictionary(i => i.Kp.Id);
        var next = edges
            .Where(e => byId.ContainsKey(e.FromKpId) && byId.ContainsKey(e.ToKpId))
            .ToLookup(e => e.FromKpId, e => e.ToKpId);

        var indegree = items.ToDictionary(i => i.Kp.Id, _ => 0);
        foreach (var target in next.SelectMany(g => g))
        {
            indegree[target]++;
        }

        var ready = items.Where(i => indegree[i.Kp.Id] == 0).ToList();
        var result = new List<(KnowledgePoint Kp, KpMasterySnapshot Snapshot, MasteryGrade Grade, int Impact)>();

        while (ready.Any())
        {
            ready = ready.OrderByDescending(i => i.Impact).ThenBy(i => i.Snapshot.Mastery).ToList();
            var current = ready[0];
            ready.RemoveAt(0);
            result.Add(current);

            foreach (var target in next[current.Kp.Id])
            {
                if (--indegree[target] == 0 && byId.TryGetValue(target, out var entry))
                {
                    ready.Add(entry);
                }
            }
        }

        if (result.Count != items.Count)
        {
            result.AddRange(items.Where(i => result.All(r => r.Kp.Id != i.Kp.Id))
                .OrderBy(i => i.Snapshot.Mastery));
        }

        return result;
    }
}
