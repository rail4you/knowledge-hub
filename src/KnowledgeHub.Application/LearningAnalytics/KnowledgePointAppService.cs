using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.LearningAnalytics.Dtos;
using KnowledgeHub.LearningAnalytics.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 知识点管理：归一化闭环（确认灰区 / 合并 / 拆分）+ 习题关联。
/// 合并后源知识点保留但 <c>CanonicalId</c> 指向目标，列表默认隐藏，历史数据可追溯。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class KnowledgePointAppService : KnowledgeHubAppService, IKnowledgePointAppService
{
    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<ExerciseKnowledgePoint, Guid> _mapRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentKpMastery, Guid> _masteryRepository;
    private readonly IRepository<KpPrerequisite, Guid> _prerequisiteRepository;
    private readonly KnowledgeEmbeddingProvider _embeddingProvider;
    private readonly KnowledgePointVectorStore _vectorStore;
    private readonly ThresholdResolver _thresholdResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public KnowledgePointAppService(
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<ExerciseKnowledgePoint, Guid> mapRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentKpMastery, Guid> masteryRepository,
        IRepository<KpPrerequisite, Guid> prerequisiteRepository,
        KnowledgeEmbeddingProvider embeddingProvider,
        KnowledgePointVectorStore vectorStore,
        ThresholdResolver thresholdResolver,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IServiceProvider serviceProvider,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _kpRepository = kpRepository;
        _mapRepository = mapRepository;
        _exerciseRepository = exerciseRepository;
        _chapterRepository = chapterRepository;
        _courseRepository = courseRepository;
        _masteryRepository = masteryRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _thresholdResolver = thresholdResolver;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
        _serviceProvider = serviceProvider;
        _unitOfWorkManager = unitOfWorkManager;
    }

    // ══════════════════════════════════════════════════════════
    //  列表 / 详情
    // ══════════════════════════════════════════════════════════

    public async Task<PagedResultDto<KnowledgePointDetailDto>> GetListAsync(GetKnowledgePointListInput input)
    {
        var all = (await _kpRepository.GetListAsync(k => k.CourseId == input.CourseId))
            .Where(k => k.CanonicalId == null) // 已合并的隐藏
            .ToList();

        if (input.Status.HasValue)
        {
            var status = input.Status.Value == 1 ? KnowledgePointStatus.PendingReview : KnowledgePointStatus.Confirmed;
            all = all.Where(k => k.Status == status).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.Source))
        {
            all = all.Where(k => string.Equals(k.Source.ToString(), input.Source, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim();
            all = all.Where(k =>
                k.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (k.Description ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var totalCount = all.Count;
        var ordered = all
            .OrderBy(k => k.Status) // 灰区排前面，方便处理
            .ThenBy(k => k.SyllabusOrder)
            .ThenBy(k => k.Name, StringComparer.Ordinal)
            .ToList();

        var page = ordered
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount)
            .ToList();

        var dtos = await BuildDetailsAsync(page, input.CourseId);
        return new PagedResultDto<KnowledgePointDetailDto>(totalCount, dtos);
    }

    public async Task<KnowledgePointDetailDto> GetAsync(Guid id)
    {
        var kp = await _kpRepository.GetAsync(id);
        var list = await BuildDetailsAsync(new List<KnowledgePoint> { kp }, kp.CourseId);
        return list.First();
    }

    private async Task<List<KnowledgePointDetailDto>> BuildDetailsAsync(List<KnowledgePoint> kps, Guid courseId)
    {
        if (!kps.Any())
        {
            return new List<KnowledgePointDetailDto>();
        }

        var kpIds = kps.Select(k => k.Id).ToList();

        var links = await _mapRepository.GetListAsync(m => kpIds.Contains(m.KnowledgePointId));
        var exerciseCount = links.GroupBy(l => l.KnowledgePointId).ToDictionary(g => g.Key, g => g.Count());

        var prerequisites = await _prerequisiteRepository.GetListAsync(e =>
            kpIds.Contains(e.FromKpId) || kpIds.Contains(e.ToKpId));

        var chapterIds = kps.Where(k => k.ChapterId.HasValue).Select(k => k.ChapterId!.Value).Distinct().ToList();
        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        // 被合并指向的名称（用于展示 CanonicalId 的目标）
        var canonicalIds = kps.Where(k => k.CanonicalId.HasValue).Select(k => k.CanonicalId!.Value).Distinct().ToList();
        var canonicalNames = canonicalIds.Any()
            ? (await _kpRepository.GetListAsync(k => canonicalIds.Contains(k.Id))).ToDictionary(k => k.Id, k => k.Name)
            : new Dictionary<Guid, string>();

        // 判定线（知识点级覆盖 → 课程级）：列表里直接给出生效线与是否覆盖，便于前端标注
        var thresholds = await _thresholdResolver.ResolveAllAsync(courseId);

        return kps.Select(k => new KnowledgePointDetailDto
        {
            Id = k.Id,
            CourseId = k.CourseId,
            ChapterId = k.ChapterId,
            ChapterTitle = k.ChapterId.HasValue ? chapters.GetValueOrDefault(k.ChapterId.Value) : null,
            Name = k.Name,
            Description = k.Description,
            SyllabusOrder = k.SyllabusOrder,
            Status = (int)k.Status,
            Source = k.Source.ToString(),
            CanonicalId = k.CanonicalId,
            CanonicalName = k.CanonicalId.HasValue ? canonicalNames.GetValueOrDefault(k.CanonicalId.Value) : null,
            ExerciseCount = exerciseCount.GetValueOrDefault(k.Id),
            PrerequisiteOutCount = prerequisites.Count(e => e.FromKpId == k.Id),
            PrerequisiteInCount = prerequisites.Count(e => e.ToKpId == k.Id),
            HasEmbedding = k.Embedding is { Length: > 0 },
            HasCustomThreshold = thresholds.Overrides.ContainsKey(k.Id),
            TargetLine = thresholds.For(k.Id).Ok,
            WeakLine = thresholds.For(k.Id).Weak,
            CreationTime = k.CreationTime,
        }).ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  编辑 / 确认
    // ══════════════════════════════════════════════════════════

    public async Task<KnowledgePointDetailDto> UpdateAsync(Guid id, UpdateKnowledgePointInput input)
    {
        var kp = await _kpRepository.GetAsync(id);
        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new UserFriendlyException("知识点名称不能为空");
        }

        if (!string.Equals(name, kp.Name, StringComparison.Ordinal))
        {
            var duplicated = await _kpRepository.GetListAsync(k =>
                k.CourseId == kp.CourseId && k.Id != kp.Id && k.CanonicalId == null && k.Name == name);
            if (duplicated.Any())
            {
                throw new UserFriendlyException($"已存在同名知识点「{name}」，可直接合并到它");
            }

            kp.Name = name;
            kp.Embedding = null; // 名称变了，向量失效，等待重建
        }

        kp.ChapterId = input.ChapterId;
        kp.Description = input.Description;
        kp.SyllabusOrder = input.SyllabusOrder;
        kp.Source = KpMapSource.Manual;

        await _kpRepository.UpdateAsync(kp, autoSave: true);
        return await GetAsync(kp.Id);
    }

    public async Task<KnowledgePointDetailDto> ConfirmAsync(Guid id)
    {
        var kp = await _kpRepository.GetAsync(id);
        kp.Status = KnowledgePointStatus.Confirmed;
        await _kpRepository.UpdateAsync(kp, autoSave: true);
        return await GetAsync(kp.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var kp = await _kpRepository.GetAsync(id);

        await _mapRepository.DeleteAsync(m => m.KnowledgePointId == kp.Id);
        await _prerequisiteRepository.DeleteAsync(e => e.FromKpId == kp.Id || e.ToKpId == kp.Id);

        // 掌握度数据随知识点一起清理，避免产生无归属的孤立记录
        await _masteryRepository.DeleteAsync(m => m.KnowledgePointId == kp.Id);

        await _vectorStore.ClearAsync(new[] { kp.Id });

        await _kpRepository.DeleteAsync(kp);
    }

    // ══════════════════════════════════════════════════════════
    //  合并 / 拆分
    // ══════════════════════════════════════════════════════════

    public async Task<KnowledgePointMergeResultDto> MergeAsync(MergeKnowledgePointsInput input)
    {
        if (input.SourceId == input.TargetId)
        {
            throw new UserFriendlyException("不能合并到自身");
        }

        var source = await _kpRepository.GetAsync(input.SourceId);
        var target = await _kpRepository.GetAsync(input.TargetId);
        if (source.CourseId != target.CourseId)
        {
            throw new UserFriendlyException("只能合并同一课程下的知识点");
        }

        var result = new KnowledgePointMergeResultDto { SourceId = source.Id, TargetId = target.Id };

        // 1) 习题映射：目标已有同题 → 权重相加；否则改挂到目标
        var sourceLinks = await _mapRepository.GetListAsync(m => m.KnowledgePointId == source.Id);
        var targetLinks = await _mapRepository.GetListAsync(m => m.KnowledgePointId == target.Id);
        var targetByExercise = targetLinks.GroupBy(l => l.ExerciseId).ToDictionary(g => g.Key, g => g.First());
        var affectedExercises = new HashSet<Guid>();

        foreach (var link in sourceLinks)
        {
            affectedExercises.Add(link.ExerciseId);
            if (targetByExercise.TryGetValue(link.ExerciseId, out var existing))
            {
                existing.Weight += link.Weight;
                await _mapRepository.UpdateAsync(existing, autoSave: true);
                await _mapRepository.DeleteAsync(link, autoSave: true);
                result.MergedExerciseLinks++;
            }
            else
            {
                link.KnowledgePointId = target.Id;
                await _mapRepository.UpdateAsync(link, autoSave: true);
                result.MovedExerciseLinks++;
            }
        }

        // 2) 掌握度：按证据量加权合并
        var sourceMasteries = await _masteryRepository.GetListAsync(m => m.KnowledgePointId == source.Id);
        var targetMasteries = await _masteryRepository.GetListAsync(m => m.KnowledgePointId == target.Id);
        var targetMasteryByStudent = targetMasteries.GroupBy(m => m.StudentId).ToDictionary(g => g.Key, g => g.First());

        foreach (var mastery in sourceMasteries)
        {
            if (targetMasteryByStudent.TryGetValue(mastery.StudentId, out var existing))
            {
                var totalEvidence = existing.Evidence + mastery.Evidence;
                existing.Mastery = totalEvidence > 0
                    ? (existing.Mastery * existing.Evidence + mastery.Mastery * mastery.Evidence) / totalEvidence
                    : (existing.Mastery + mastery.Mastery) / 2;
                existing.Evidence = totalEvidence;
                existing.UpdatedAt = DateTime.UtcNow;
                await _masteryRepository.UpdateAsync(existing, autoSave: true);
                await _masteryRepository.DeleteAsync(mastery, autoSave: true);
            }
            else
            {
                mastery.KnowledgePointId = target.Id;
                await _masteryRepository.UpdateAsync(mastery, autoSave: true);
            }

            result.MovedMasteries++;
        }

        // 3) 先修关系：重指向 + 去重
        var edges = await _prerequisiteRepository.GetListAsync(e =>
            e.FromKpId == source.Id || e.ToKpId == source.Id);
        foreach (var edge in edges)
        {
            var from = edge.FromKpId == source.Id ? target.Id : edge.FromKpId;
            var to = edge.ToKpId == source.Id ? target.Id : edge.ToKpId;

            if (from == to)
            {
                await _prerequisiteRepository.DeleteAsync(edge, autoSave: true);
                continue;
            }

            var duplicated = await _prerequisiteRepository.GetListAsync(e =>
                e.Id != edge.Id && e.FromKpId == from && e.ToKpId == to);
            if (duplicated.Any())
            {
                await _prerequisiteRepository.DeleteAsync(edge, autoSave: true);
                continue;
            }

            edge.FromKpId = from;
            edge.ToKpId = to;
            await _prerequisiteRepository.UpdateAsync(edge, autoSave: true);
            result.MovedPrerequisites++;
        }

        // 4) 源知识点归档（保留行以便追溯，列表默认隐藏）
        source.CanonicalId = target.Id;
        source.Status = KnowledgePointStatus.Confirmed;
        await _kpRepository.UpdateAsync(source, autoSave: true);
        await _vectorStore.ClearAsync(new[] { source.Id });

        await NormalizeWeightsAsync(affectedExercises);

        result.Message = $"已把「{source.Name}」合并到「{target.Name}」：迁移习题关联 {result.MovedExerciseLinks} 条、" +
                         $"合并同题关联 {result.MergedExerciseLinks} 条、掌握度 {result.MovedMasteries} 条、" +
                         $"先修关系 {result.MovedPrerequisites} 条";
        return result;
    }

    public async Task<KnowledgePointDetailDto> SplitAsync(SplitKnowledgePointInput input)
    {
        var kp = await _kpRepository.GetAsync(input.KnowledgePointId);
        var newName = (input.NewName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new UserFriendlyException("新知识点名称不能为空");
        }

        if (!input.ExerciseIds.Any())
        {
            throw new UserFriendlyException("请至少选择一道要拆出的习题");
        }

        var duplicated = await _kpRepository.GetListAsync(k =>
            k.CourseId == kp.CourseId && k.CanonicalId == null && k.Name == newName);
        if (duplicated.Any())
        {
            throw new UserFriendlyException($"已存在同名知识点「{newName}」");
        }

        var created = new KnowledgePoint(_guidGenerator.Create(), kp.CourseId, newName)
        {
            ChapterId = input.ChapterId ?? kp.ChapterId,
            SyllabusOrder = kp.SyllabusOrder,
            Source = KpMapSource.Manual,
            Status = KnowledgePointStatus.Confirmed,
        };
        await _kpRepository.InsertAsync(created, autoSave: true);

        var links = await _mapRepository.GetListAsync(m =>
            m.KnowledgePointId == kp.Id && input.ExerciseIds.Contains(m.ExerciseId));

        var affected = new HashSet<Guid>();
        foreach (var link in links)
        {
            affected.Add(link.ExerciseId);
            link.KnowledgePointId = created.Id;
            link.Source = KpMapSource.Manual;
            await _mapRepository.UpdateAsync(link, autoSave: true);
        }

        await NormalizeWeightsAsync(affected);
        return await GetAsync(created.Id);
    }

    // ══════════════════════════════════════════════════════════
    //  习题关联
    // ══════════════════════════════════════════════════════════

    public async Task<List<KpExerciseLinkDto>> GetExerciseLinksAsync(Guid knowledgePointId)
    {
        var links = await _mapRepository.GetListAsync(m => m.KnowledgePointId == knowledgePointId);
        if (!links.Any())
        {
            return new List<KpExerciseLinkDto>();
        }

        var exerciseIds = links.Select(l => l.ExerciseId).Distinct().ToList();
        var exercises = (await _exerciseRepository.GetListAsync(e => exerciseIds.Contains(e.Id)))
            .ToDictionary(e => e.Id);

        var chapterIds = exercises.Values.Where(e => e.ChapterId.HasValue)
            .Select(e => e.ChapterId!.Value).Distinct().ToList();
        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        var kp = await _kpRepository.FindAsync(knowledgePointId);

        return links
            .Select(l =>
            {
                exercises.TryGetValue(l.ExerciseId, out var exercise);
                return new KpExerciseLinkDto
                {
                    Id = l.Id,
                    ExerciseId = l.ExerciseId,
                    ExerciseTitle = exercise?.Title ?? "（题目已删除）",
                    QuestionContent = Truncate(exercise?.QuestionContent, 160),
                    ChapterId = exercise?.ChapterId,
                    ChapterTitle = exercise?.ChapterId != null
                        ? chapters.GetValueOrDefault(exercise.ChapterId.Value)
                        : null,
                    ExerciseScore = exercise?.Score ?? 0,
                    Weight = Math.Round(l.Weight, 4),
                    Source = l.Source.ToString(),
                    KnowledgePointId = l.KnowledgePointId,
                    KnowledgePointName = kp?.Name ?? string.Empty,
                    KnowledgePointStatus = kp != null ? (int)kp.Status : 0,
                };
            })
            .OrderByDescending(l => l.Weight)
            .ToList();
    }

    public async Task<KpExerciseLinkDto> AddExerciseLinkAsync(AddKpExerciseLinkInput input)
    {
        var kp = await _kpRepository.GetAsync(input.KnowledgePointId);
        var exercise = await _exerciseRepository.FindAsync(input.ExerciseId)
                       ?? throw new UserFriendlyException("习题不存在");
        if (exercise.CourseId != kp.CourseId)
        {
            throw new UserFriendlyException("只能关联本课程的习题");
        }

        var existing = await _mapRepository.GetListAsync(m =>
            m.KnowledgePointId == kp.Id && m.ExerciseId == exercise.Id);
        var link = existing.FirstOrDefault();
        if (link == null)
        {
            link = new ExerciseKnowledgePoint(_guidGenerator.Create(), exercise.Id, kp.Id)
            {
                Weight = input.Weight <= 0 ? 1 : input.Weight,
                Source = KpMapSource.Manual,
            };
            await _mapRepository.InsertAsync(link, autoSave: true);
        }
        else
        {
            link.Weight = input.Weight <= 0 ? link.Weight : input.Weight;
            link.Source = KpMapSource.Manual;
            await _mapRepository.UpdateAsync(link, autoSave: true);
        }

        await NormalizeWeightsAsync(new[] { exercise.Id });
        var links = await GetExerciseLinksAsync(kp.Id);
        return links.First(l => l.Id == link.Id);
    }

    public async Task<KpExerciseLinkDto> UpdateExerciseLinkAsync(UpdateKpExerciseLinkInput input)
    {
        var link = await _mapRepository.FindAsync(input.LinkId)
                   ?? throw new UserFriendlyException("关联不存在，请刷新后重试");

        link.Weight = Math.Clamp(input.Weight, 0.01, 1);
        link.Source = KpMapSource.Manual;
        await _mapRepository.UpdateAsync(link, autoSave: true);

        await NormalizeWeightsAsync(new[] { link.ExerciseId });
        var links = await GetExerciseLinksAsync(link.KnowledgePointId);
        return links.FirstOrDefault(l => l.Id == link.Id) ?? new KpExerciseLinkDto { Id = link.Id };
    }

    public async Task RemoveExerciseLinkAsync(Guid linkId)
    {
        var link = await _mapRepository.FindAsync(linkId);
        if (link == null)
        {
            return;
        }

        var exerciseId = link.ExerciseId;
        await _mapRepository.DeleteAsync(link, autoSave: true);
        await NormalizeWeightsAsync(new[] { exerciseId });
    }

    /// <summary>同一题的多条知识点权重归一化（和为 1）。</summary>
    private async Task NormalizeWeightsAsync(IEnumerable<Guid> exerciseIds)
    {
        var ids = exerciseIds.Distinct().ToList();
        if (!ids.Any())
        {
            return;
        }

        // 与「习题管理」一致：禁用多租户过滤器，避免漏掉跨租户历史数据的关联
        using (DataFilter.Disable<IMultiTenant>())
        {
            var links = await _mapRepository.GetListAsync(m => ids.Contains(m.ExerciseId));
            foreach (var group in links.GroupBy(l => l.ExerciseId))
            {
                var total = group.Sum(l => l.Weight);
                if (total <= 0)
                {
                    var even = Math.Round(1.0 / group.Count(), 4);
                    foreach (var link in group)
                    {
                        link.Weight = even;
                        await _mapRepository.UpdateAsync(link, autoSave: true);
                    }

                    continue;
                }

                if (Math.Abs(total - 1) < 0.0001)
                {
                    continue;
                }

                foreach (var link in group)
                {
                    link.Weight = Math.Round(link.Weight / total, 4);
                    await _mapRepository.UpdateAsync(link, autoSave: true);
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  习题 ↔ 知识点 映射（一题可对应多个知识点）
    // ══════════════════════════════════════════════════════════

    public async Task<List<KpExerciseMappingDto>> GetExerciseMappingsAsync(Guid courseId)
    {
        // 与「习题管理」保持同一查询范围：课程与其习题可能分属不同租户（历史跨租户选课），
        // 这里同样禁用多租户过滤器，避免教师看到空列表。
        using (DataFilter.Disable<IMultiTenant>())
        {
            var exercises = (await _exerciseRepository.GetListAsync(e => e.CourseId == courseId))
                .OrderBy(e => e.CreationTime)
                .ToList();
            if (!exercises.Any())
            {
                return new List<KpExerciseMappingDto>();
            }

            var exerciseIds = exercises.Select(e => e.Id).ToList();
            var maps = await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId));

            var kpIds = maps.Select(m => m.KnowledgePointId).Distinct().ToList();
            var kps = kpIds.Any()
                ? (await _kpRepository.GetListAsync(k => kpIds.Contains(k.Id))).ToDictionary(k => k.Id)
                : new Dictionary<Guid, KnowledgePoint>();

            var chapters = (await _chapterRepository.GetListAsync(c => c.CourseId == courseId))
                .ToDictionary(c => c.Id, c => c.Title);

            return exercises.Select(exercise =>
            {
                var chapterTitle = exercise.ChapterId.HasValue
                    ? chapters.GetValueOrDefault(exercise.ChapterId.Value)
                    : null;

                var links = maps
                    .Where(m => m.ExerciseId == exercise.Id)
                    .Select(m =>
                    {
                        kps.TryGetValue(m.KnowledgePointId, out var kp);
                        return new KpExerciseLinkDto
                        {
                            Id = m.Id,
                            ExerciseId = m.ExerciseId,
                            ExerciseTitle = exercise.Title,
                            QuestionContent = Truncate(exercise.QuestionContent, 160),
                            ChapterId = exercise.ChapterId,
                            ChapterTitle = chapterTitle,
                            ExerciseScore = exercise.Score,
                            Weight = Math.Round(m.Weight, 4),
                            Source = m.Source.ToString(),
                            KnowledgePointId = m.KnowledgePointId,
                            KnowledgePointName = kp?.Name ?? "（知识点已删除）",
                            KnowledgePointStatus = kp != null ? (int)kp.Status : 0,
                        };
                    })
                    .OrderByDescending(l => l.Weight)
                    .ToList();

                return new KpExerciseMappingDto
                {
                    ExerciseId = exercise.Id,
                    ExerciseTitle = exercise.Title,
                    QuestionContent = Truncate(exercise.QuestionContent, 160),
                    ChapterId = exercise.ChapterId,
                    ChapterTitle = chapterTitle,
                    ExerciseScore = exercise.Score,
                    Links = links,
                };
            }).ToList();
        }
    }

    /// <summary>
    /// 批量保存某道习题的知识点关联：
    /// 清单外的关联会被移除，清单内的权重会被批量写入，最后按同一题归一化为 1。
    /// </summary>
    public async Task<KpExerciseMappingDto> SaveExerciseMappingsAsync(SaveKpExerciseLinksInput input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var exercise = await _exerciseRepository.FindAsync(input.ExerciseId)
                           ?? throw new UserFriendlyException("习题不存在，请刷新后重试");

            var wanted = input.Links
                .Where(l => l.KnowledgePointId != Guid.Empty)
                .GroupBy(l => l.KnowledgePointId)
                .Select(g => g.First())
                .ToList();

            var kpIds = wanted.Select(l => l.KnowledgePointId).ToList();
            var kps = kpIds.Any()
                ? await _kpRepository.GetListAsync(k => kpIds.Contains(k.Id))
                : new List<KnowledgePoint>();

            foreach (var kp in kps)
            {
                if (kp.CourseId != exercise.CourseId)
                {
                    throw new UserFriendlyException("只能关联本课程的知识点");
                }
            }

            var kpById = kps.ToDictionary(k => k.Id);
            var existing = await _mapRepository.GetListAsync(m => m.ExerciseId == exercise.Id);
            var existingByKp = existing
                .GroupBy(m => m.KnowledgePointId)
                .ToDictionary(g => g.Key, g => g.First());

            // 1) 移除不在期望清单中的关联
            foreach (var link in existing.Where(l => !kpIds.Contains(l.KnowledgePointId)))
            {
                await _mapRepository.DeleteAsync(link, autoSave: true);
            }

            // 2) 批量写入（新增关联 + 更新权重）
            foreach (var item in wanted)
            {
                if (!kpById.ContainsKey(item.KnowledgePointId))
                {
                    continue;
                }

                var weight = item.Weight <= 0 ? 1 : Math.Min(item.Weight, 1);

                if (existingByKp.TryGetValue(item.KnowledgePointId, out var link))
                {
                    link.Weight = weight;
                    link.Source = KpMapSource.Manual;
                    await _mapRepository.UpdateAsync(link, autoSave: true);
                }
                else
                {
                    await _mapRepository.InsertAsync(
                        new ExerciseKnowledgePoint(_guidGenerator.Create(), exercise.Id, item.KnowledgePointId)
                        {
                            Weight = weight,
                            Source = KpMapSource.Manual,
                        }, autoSave: true);
                }
            }

            // 3) 同一题的多知识点权重归一化为 1
            await NormalizeWeightsAsync(new[] { exercise.Id });

            var mappings = await GetExerciseMappingsAsync(exercise.CourseId);
            return mappings.FirstOrDefault(m => m.ExerciseId == exercise.Id)
                   ?? new KpExerciseMappingDto
                   {
                       ExerciseId = exercise.Id,
                       ExerciseTitle = exercise.Title,
                       ChapterId = exercise.ChapterId,
                   };
        }
    }

    /// <summary>
    /// 课程全部知识点（供「配置习题知识点」弹窗勾选）。
    /// 与习题映射同一查询范围：禁用多租户过滤器。
    /// </summary>
    public async Task<List<KnowledgePointDetailDto>> GetCourseKnowledgePointsAsync(Guid courseId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var kps = (await _kpRepository.GetListAsync(k => k.CourseId == courseId))
                .Where(k => k.CanonicalId == null)
                .ToList();
            if (!kps.Any())
            {
                return new List<KnowledgePointDetailDto>();
            }

            var details = await BuildDetailsAsync(kps, courseId);
            return details
                .OrderBy(d => d.SyllabusOrder)
                .ThenBy(d => d.Name, StringComparer.Ordinal)
                .ToList();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  全量生成（章节 → 知识点、习题 → 知识点关联、重建语义索引）
    // ══════════════════════════════════════════════════════════

    public async Task<KpGenerationResultDto> GenerateAsync(Guid courseId)
    {
        // 与「习题管理」同一查询范围：课程可能属于其它租户（教师在宿主侧统一管理）
        using (DataFilter.Disable<IMultiTenant>())
        {
            var course = await _courseRepository.FindAsync(courseId)
                         ?? throw new UserFriendlyException("课程不存在");

            var chapterCount = (await _chapterRepository.GetListAsync(c => c.CourseId == courseId)).Count;
            var exerciseCount = (await _exerciseRepository.GetListAsync(e => e.CourseId == courseId)).Count;
            if (chapterCount == 0 && exerciseCount == 0)
            {
                throw new UserFriendlyException("该课程还没有章节和习题，先建章节或习题再生成知识点");
            }

            var result = new KpGenerationResultDto
            {
                CourseId = courseId,
                Queued = true,
                ChapterCount = chapterCount,
                ExerciseCount = exerciseCount,
                Message = $"已提交知识点全量生成任务：{chapterCount} 个章节 → 知识点、{exerciseCount} 道习题 → 知识点关联，" +
                          $"完成后重建语义索引，稍后刷新查看结果",
            };

            await RunAfterCommitAsync(async () =>
            {
                var queue = _serviceProvider.GetService(typeof(ILearningAnalyticsJobQueue)) as ILearningAnalyticsJobQueue;
                if (queue != null)
                {
                    queue.EnqueueKnowledgePointGeneration(courseId, KpGenerationMode.Full, CurrentTenant.Id);
                    return;
                }

                // 未注册队列（如单元测试）→ 同步执行，保证功能可用
                var job = (KpGenerationJob)_serviceProvider.GetService(typeof(KpGenerationJob))!;
                await job.ExecuteAsync(courseId, KpGenerationMode.Full, CurrentTenant.Id);
            });

            return result;
        }
    }

    /// <summary>UoW 提交后再执行（入队），避免后台任务读不到未提交数据。</summary>
    private Task RunAfterCommitAsync(Func<Task> action)
    {
        var uow = _unitOfWorkManager.Current;
        if (uow == null)
        {
            return action();
        }

        uow.OnCompleted(action);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════
    //  合并建议 / 向量
    // ══════════════════════════════════════════════════════════

    public async Task<List<KpMergeSuggestionDto>> GetMergeSuggestionsAsync(
        Guid courseId,
        double threshold = 0.75,
        string scope = "course")
    {
        var crossCourse = string.Equals(scope, "tenant", StringComparison.OrdinalIgnoreCase);

        var kps = (await _kpRepository.GetListAsync(k => k.CourseId == courseId))
            .Where(k => k.CanonicalId == null)
            .ToList();
        if (kps.Count < 2)
        {
            return new List<KpMergeSuggestionDto>();
        }

        // 缺向量的先补上（向量服务不可用时 provider=none，走字符近似）
        var missing = kps.Where(k => k.Embedding is not { Length: > 0 }).ToList();
        var providerUsed = "char";
        if (missing.Any())
        {
            var embeddingResult = await _embeddingProvider.EmbedAsync(missing.Select(k => k.Name).ToList());
            if (embeddingResult.IsAvailable)
            {
                providerUsed = embeddingResult.Provider;
                for (var i = 0; i < missing.Count; i++)
                {
                    var vector = embeddingResult.Vectors[i];
                    if (vector is { Length: > 0 })
                    {
                        missing[i].Embedding = vector;
                        await _kpRepository.UpdateAsync(missing[i], autoSave: true);
                    }
                }
            }
        }

        var usePgvector = await _vectorStore.IsAvailableAsync();
        var suggestions = usePgvector
            ? await BuildSuggestionsByVectorAsync(kps, courseId, crossCourse, threshold)
            : BuildSuggestionsInProcess(kps, threshold, providerUsed);

        // 同一对知识点只保留相似度最高的一条
        return suggestions
            .GroupBy(s => string.CompareOrdinal(s.SourceId.ToString(), s.TargetId.ToString()) < 0
                ? $"{s.SourceId}|{s.TargetId}"
                : $"{s.TargetId}|{s.SourceId}")
            .Select(g => g.OrderByDescending(s => s.Similarity).First())
            .OrderByDescending(s => s.Similarity)
            .Take(50)
            .ToList();
    }

    /// <summary>pgvector 路径：每个知识点做一次 ANN 检索（scope=tenant 时跨课程），命中即候选。</summary>
    private async Task<List<KpMergeSuggestionDto>> BuildSuggestionsByVectorAsync(
        List<KnowledgePoint> kps,
        Guid courseId,
        bool crossCourse,
        double threshold)
    {
        var byId = kps.ToDictionary(k => k.Id);
        var suggestions = new List<KpMergeSuggestionDto>();

        foreach (var kp in kps)
        {
            if (kp.Embedding is not { Length: > 0 } vector)
            {
                continue;
            }

            var hits = await _vectorStore.SearchAsync(
                vector,
                _currentTenant.Id,
                crossCourse ? null : courseId,
                take: 8);

            foreach (var (hitId, similarity) in hits)
            {
                if (hitId == kp.Id || similarity < threshold)
                {
                    continue;
                }

                // 跨课程时命中项可能不属于本课程，补查一次拿名称
                KnowledgePoint? other;
                if (!byId.TryGetValue(hitId, out other))
                {
                    other = await _kpRepository.FindAsync(hitId);
                    if (other == null || other.CanonicalId != null)
                    {
                        continue;
                    }
                }

                var source = kp.Status == KnowledgePointStatus.PendingReview ? kp : other;
                var target = ReferenceEquals(source, kp) ? other : kp;

                suggestions.Add(new KpMergeSuggestionDto
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    SourceStatus = (int)source.Status,
                    TargetId = target.Id,
                    TargetName = target.Name,
                    TargetStatus = (int)target.Status,
                    Similarity = Math.Round(similarity, 4),
                    Method = "embedding",
                });
            }
        }

        return suggestions;
    }

    /// <summary>回退路径：进程内两两比较（有向量用余弦，否则字符近似）。</summary>
    private static List<KpMergeSuggestionDto> BuildSuggestionsInProcess(
        List<KnowledgePoint> kps,
        double threshold,
        string providerUsed)
    {
        var suggestions = new List<KpMergeSuggestionDto>();
        for (var i = 0; i < kps.Count; i++)
        {
            for (var j = i + 1; j < kps.Count; j++)
            {
                var a = kps[i];
                var b = kps[j];
                var (similarity, method) = Compare(a, b, providerUsed);

                if (similarity < threshold)
                {
                    continue;
                }

                var source = a.Status == KnowledgePointStatus.PendingReview ? a : b;
                var target = ReferenceEquals(source, a) ? b : a;

                suggestions.Add(new KpMergeSuggestionDto
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    SourceStatus = (int)source.Status,
                    TargetId = target.Id,
                    TargetName = target.Name,
                    TargetStatus = (int)target.Status,
                    Similarity = Math.Round(similarity, 4),
                    Method = method,
                });
            }
        }

        return suggestions;
    }

    // ══════════════════════════════════════════════════════════
    //  知识点图谱
    // ══════════════════════════════════════════════════════════

    public async Task<KnowledgePointGraphDto> GetGraphAsync(Guid courseId)
    {
        var course = await _courseRepository.FindAsync(courseId);
        var courseKps = (await _kpRepository.GetListAsync(k => k.CourseId == courseId)).ToList();

        var result = new KnowledgePointGraphDto
        {
            CourseId = courseId,
            CourseName = course?.Title ?? string.Empty,
        };

        if (!courseKps.Any())
        {
            return result;
        }

        var courseKpIds = courseKps.Select(k => k.Id).ToHashSet();

        // 先修边：任一端属于本课程（另一端可能是其他课程的知识点，作为「外部前置」展示）
        var edges = await _prerequisiteRepository.GetListAsync(e =>
            courseKpIds.Contains(e.FromKpId) || courseKpIds.Contains(e.ToKpId));

        var neighborIds = edges
            .SelectMany(e => new[] { e.FromKpId, e.ToKpId })
            .Where(id => !courseKpIds.Contains(id))
            .Distinct()
            .ToList();

        var neighborKps = neighborIds.Any()
            ? (await _kpRepository.GetListAsync(k => neighborIds.Contains(k.Id))).ToList()
            : new List<KnowledgePoint>();

        var neighborCourseIds = neighborKps.Select(k => k.CourseId).Distinct().ToList();
        var neighborCourses = neighborCourseIds.Any()
            ? (await _courseRepository.GetListAsync(c => neighborCourseIds.Contains(c.Id)))
                .ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        // 组装节点（本课程 = 全部含已归档；外部 = 跨课程前置）
        var details = await BuildDetailsAsync(courseKps.Concat(neighborKps).ToList(), courseId);
        var detailById = details.ToDictionary(d => d.Id);

        foreach (var kp in courseKps)
        {
            if (!detailById.TryGetValue(kp.Id, out var detail))
            {
                continue;
            }

            result.Nodes.Add(new KnowledgePointGraphNodeDto
            {
                Kp = detail,
                IsExternal = false,
                CourseTitle = course?.Title,
            });

            if (kp.CanonicalId != null)
            {
                result.MergedNodeCount++;
            }
        }

        foreach (var kp in neighborKps)
        {
            if (!detailById.TryGetValue(kp.Id, out var detail))
            {
                continue;
            }

            result.Nodes.Add(new KnowledgePointGraphNodeDto
            {
                Kp = detail,
                IsExternal = true,
                CourseTitle = neighborCourses.GetValueOrDefault(kp.CourseId, string.Empty),
            });
        }

        result.ExternalNodeCount = result.Nodes.Count(n => n.IsExternal);

        // 先修边
        foreach (var edge in edges)
        {
            result.Edges.Add(new KnowledgePointGraphEdgeDto
            {
                FromId = edge.FromKpId,
                ToId = edge.ToKpId,
                Type = "prerequisite",
                Status = edge.Status == PrerequisiteStatus.Confirmed ? 1 : 0,
                Strength = Math.Round(edge.Strength, 4),
                Note = edge.Condition,
            });

            if (edge.Status != PrerequisiteStatus.Confirmed)
            {
                result.ProposedEdgeCount++;
            }
        }

        // 合并边（已归档知识点 → 标准知识点）
        foreach (var kp in courseKps.Where(k => k.CanonicalId.HasValue))
        {
            if (detailById.ContainsKey(kp.CanonicalId!.Value))
            {
                result.Edges.Add(new KnowledgePointGraphEdgeDto
                {
                    FromId = kp.Id,
                    ToId = kp.CanonicalId.Value,
                    Type = "merge",
                    Status = 1,
                    Strength = 1,
                    Note = "已合并",
                });
            }
        }

        // 拓扑分层：用于前端从左到右/上到下布局（先修在前）
        result.Nodes = AssignLevels(result.Nodes, result.Edges);
        return result;
    }

    /// <summary>按先修边做最长路径分层（带环保护：最多迭代 N 轮）。</summary>
    private static List<KnowledgePointGraphNodeDto> AssignLevels(
        List<KnowledgePointGraphNodeDto> nodes,
        List<KnowledgePointGraphEdgeDto> edges)
    {
        var level = nodes.ToDictionary(n => n.Kp.Id, _ => 0);
        var prerequisites = edges
            .Where(e => e.Type == "prerequisite")
            .Select(e => (e.FromId, e.ToId))
            .ToList();

        if (!prerequisites.Any())
        {
            nodes.ForEach(n => n.Level = 0);
            return nodes;
        }

        for (var round = 0; round < nodes.Count; round++)
        {
            var changed = false;
            foreach (var (from, to) in prerequisites)
            {
                if (!level.ContainsKey(from) || !level.ContainsKey(to))
                {
                    continue;
                }

                if (level[to] < level[from] + 1)
                {
                    level[to] = level[from] + 1;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        // 环（脏数据）产生的超深层级做归一化，避免布局拉得过长
        var maxLevel = level.Values.DefaultIfEmpty(0).Max();
        nodes.ForEach(n => n.Level = Math.Min(level[n.Kp.Id], maxLevel));
        return nodes;
    }

    public async Task<KnowledgeVectorStatusDto> GetVectorStatusAsync(Guid? courseId = null)
    {
        var state = await _vectorStore.GetStateAsync(_currentTenant.Id, courseId);
        var kpCount = courseId.HasValue
            ? (await _kpRepository.GetListAsync(k => k.CourseId == courseId.Value)).Count(k => k.CanonicalId == null)
            : (int)await _kpRepository.GetCountAsync();

        return new KnowledgeVectorStatusDto
        {
            Engine = state.Engine,
            Dimension = state.Dimension,
            Available = state.Available,
            EmbeddedCount = state.EmbeddedCount,
            KnowledgePointCount = kpCount,
            Message = state.Message,
        };
    }

    private static (double Similarity, string Method) Compare(KnowledgePoint a, KnowledgePoint b, string provider)
    {
        if (a.Embedding is { Length: > 0 } va && b.Embedding is { Length: > 0 } vb && va.Length == vb.Length)
        {
            double dot = 0, na = 0, nb = 0;
            for (var i = 0; i < va.Length; i++)
            {
                dot += (double)va[i] * vb[i];
                na += (double)va[i] * va[i];
                nb += (double)vb[i] * vb[i];
            }

            if (na > 0 && nb > 0)
            {
                // 向量分支：方法标记为 embedding（provider 名仅用于诊断）
                return (Math.Clamp(dot / (Math.Sqrt(na) * Math.Sqrt(nb)), 0, 1), "embedding");
            }
        }

        return (KpGenerationJob.CharJaccard(a.Name, b.Name), "char");
    }

    public async Task<RebuildEmbeddingsResultDto> RebuildEmbeddingsAsync(Guid courseId)
    {
        var kps = (await _kpRepository.GetListAsync(k => k.CourseId == courseId))
            .Where(k => k.CanonicalId == null)
            .ToList();

        var result = new RebuildEmbeddingsResultDto { Total = kps.Count };
        if (!kps.Any())
        {
            result.Message = "该课程暂无知识点";
            return result;
        }

        var embeddingResult = await _embeddingProvider.EmbedAsync(kps.Select(k => k.Name).ToList());
        result.Provider = embeddingResult.Provider;

        if (!embeddingResult.IsAvailable)
        {
            result.Message = "未配置向量服务（EmbeddingService:BaseUrl）且 Qwen embeddings 不可用，" +
                             "合并建议将退化为字符近似（Jaccard）";
            return result;
        }

        var written = new List<(Guid Id, float[] Vector)>();
        for (var i = 0; i < kps.Count; i++)
        {
            var vector = embeddingResult.Vectors[i];
            if (vector is not { Length: > 0 })
            {
                continue;
            }

            kps[i].Embedding = vector;
            await _kpRepository.UpdateAsync(kps[i], autoSave: true);
            written.Add((kps[i].Id, vector));
            result.Updated++;
        }

        // 双写 pgvector（不可用时静默跳过，业务仍能靠进程内余弦工作）
        result.VectorStored = await _vectorStore.UpsertAsync(written, _currentTenant.Id);
        var state = await _vectorStore.GetStateAsync(_currentTenant.Id, courseId);
        result.Engine = state.Engine;
        result.Dimension = state.Dimension;

        result.Message = $"已重建 {result.Updated}/{result.Total} 个知识点的语义向量" +
                         $"（provider={result.Provider}，检索引擎={result.Engine}，pgvector 入库 {result.VectorStored} 条）";
        return result;
    }

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? text : text.Length <= max ? text : text[..max];
}
