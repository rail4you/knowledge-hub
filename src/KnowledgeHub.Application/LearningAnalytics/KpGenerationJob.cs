using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.LearningAnalytics.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 知识点统一生成任务（把「AI 抽取 / 重新抽取 / 重建语义索引」合并成一个入口）。
///
/// <see cref="KpGenerationMode.Full"/> 全量刷新流程：
/// 1) 章节 → 知识点：让 LLM 判断每个章节真正考察的知识点（不可用时回退到章节标题本身）；
/// 2) 习题 → 知识点关联：让 LLM 为每道题抽取 1~3 个知识点并给权重；
/// 3) 语义索引：按需重建课程内全部知识点的向量并同步 pgvector。
///
/// 归一化策略（无向量时的可解释近似）：
/// 1) 精确同名（忽略大小写/空白）→ 复用；
/// 2) 互相包含且短名长度 ≥ 2 → 复用（如「级数收敛」⊂「级数收敛判别」）；
/// 3) 语义相似度 ≥ <see cref="KpAutoMergeThreshold"/> → 归并到已有知识点；
/// 4) 语义相似度 ≥ <see cref="KpReviewThreshold"/> → 新建但标记待确认（灰区），交给老师确认合并；
/// 5) 其余 → 新建确认态。
///
/// 老师手工维护的映射（<see cref="KpMapSource.Manual"/>）永不被覆盖。
/// 本任务不删除知识点、不删除章节，只做覆盖式刷新。
/// </summary>
public class KpGenerationJob : ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource, Guid> _resourceRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<ExerciseKnowledgePoint> _mapRepository;
    private readonly KnowledgePointExtractor _extractor;
    private readonly KnowledgeEmbeddingProvider _embeddingProvider;
    private readonly KnowledgePointVectorStore _vectorStore;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IDataFilter<IMultiTenant> _multiTenantDataFilter;
    private readonly ILogger<KpGenerationJob> _logger;

    public KpGenerationJob(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource, Guid> resourceRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<ExerciseKnowledgePoint> mapRepository,
        KnowledgePointExtractor extractor,
        KnowledgeEmbeddingProvider embeddingProvider,
        KnowledgePointVectorStore vectorStore,
        IGuidGenerator guidGenerator,
        IConfiguration configuration,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IDataFilter<IMultiTenant> multiTenantDataFilter,
        ILogger<KpGenerationJob> logger)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _resourceRepository = resourceRepository;
        _exerciseRepository = exerciseRepository;
        _kpRepository = kpRepository;
        _mapRepository = mapRepository;
        _extractor = extractor;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _multiTenantDataFilter = multiTenantDataFilter;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid courseId, KpGenerationMode mode, Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        // 与「习题管理」同一查询范围：课程 / 习题 / 知识点可能属于其它租户，
        // 后台线程没有请求上下文，必须放开多租户过滤器才能取到数据。
        using (_multiTenantDataFilter.Disable())
        {
            if (!_configuration.GetValue("LearningAnalytics:KpExtractionEnabled", true))
            {
                _logger.LogInformation("知识点生成已关闭（LearningAnalytics:KpExtractionEnabled=false）");
                return;
            }

            var course = await _courseRepository.FindAsync(courseId);
            if (course == null)
            {
                return;
            }

            // 课程内知识点（含已归档，作为归一化候选）
            var kps = new List<KnowledgePoint>(
                (await _kpRepository.GetListAsync(k => k.CourseId == courseId)).Where(k => k.CanonicalId == null));
            await EnsureEmbeddingsAsync(kps);

            var kpByName = kps
                .GroupBy(k => k.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var chapterStats = await GenerateFromChaptersAsync(course, kps, kpByName);
            var exerciseStats = await GenerateFromExercisesAsync(course, kps, kpByName, mode);

            var embedded = 0;
            if (mode == KpGenerationMode.Full)
            {
                embedded = await RebuildIndexAsync(courseId);
            }

            _logger.LogInformation(
                "知识点生成完成：course {CourseId}，模式 {Mode}，章节新增知识点 {ChapterKps}，" +
                "习题处理 {Processed}/{Total}，新增关联 {Links}，重建向量 {Embedded}",
                courseId, mode, chapterStats, exerciseStats.Processed, exerciseStats.Total, exerciseStats.Created, embedded);

            await uow.CompleteAsync();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  阶段一：章节 → 知识点
    // ══════════════════════════════════════════════════════════

    /// <summary>按章节生成知识点（AI 判断；AI 不可用时回退到章节标题）。返回新建知识点数。</summary>
    private async Task<int> GenerateFromChaptersAsync(
        Course course,
        List<KnowledgePoint> kps,
        Dictionary<string, KnowledgePoint> kpByName)
    {
        var chapters = await _chapterRepository.GetListAsync(c => c.CourseId == course.Id);
        if (!chapters.Any())
        {
            return 0;
        }

        // 叶子章节优先，避免父子章节重复造词
        var parentIds = chapters.Where(c => c.ParentId.HasValue).Select(c => c.ParentId!.Value).ToHashSet();
        var leaves = chapters.Where(c => !parentIds.Contains(c.Id)).OrderBy(c => c.SortOrder).ToList();
        if (!leaves.Any())
        {
            leaves = chapters.OrderBy(c => c.SortOrder).ToList();
        }

        var resources = await _resourceRepository.GetListAsync(r => r.CourseId == course.Id);
        var resourcesByChapter = resources
            .Where(r => r.ChapterId.HasValue)
            .GroupBy(r => r.ChapterId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Name).ToList());

        var created = 0;
        foreach (var chapter in leaves)
        {
            resourcesByChapter.TryGetValue(chapter.Id, out var resourceTitles);

            var extracted = await _extractor.ExtractFromChapterAsync(
                course.Title,
                chapter.Title,
                chapter.Description,
                resourceTitles ?? new List<string>(),
                kpByName.Keys.ToList());

            // AI 不可用 / 返回空 → 回退：章节标题本身即知识点
            if (!extracted.Any())
            {
                extracted = new List<ExtractedChapterKnowledgePoint>
                {
                    new(chapter.Title.Trim(), chapter.Description),
                };
            }

            foreach (var item in extracted)
            {
                var (kp, isNew) = await ResolveKnowledgePointAsync(
                    course.Id, item.Name, kps, kpByName, chapter.Id, item.Description);

                if (isNew)
                {
                    created++;
                }

                // 复用到已有知识点时补上章节归属（老师手工维护的不覆盖）
                if (!isNew && kp.ChapterId == null && kp.Source != KpMapSource.Manual)
                {
                    kp.ChapterId = chapter.Id;
                    await _kpRepository.UpdateAsync(kp, autoSave: true);
                }
            }
        }

        return created;
    }

    // ══════════════════════════════════════════════════════════
    //  阶段二：习题 → 知识点关联
    // ══════════════════════════════════════════════════════════

    /// <summary>按习题抽取知识点关联，返回 (处理题数, 总题数, 新增关联数)。</summary>
    private async Task<(int Processed, int Total, int Created)> GenerateFromExercisesAsync(
        Course course,
        List<KnowledgePoint> kps,
        Dictionary<string, KnowledgePoint> kpByName,
        KpGenerationMode mode)
    {
        var maxExercises = _configuration.GetValue("LearningAnalytics:KpExtractionMaxExercises", 200);

        var exercises = (await _exerciseRepository.GetListAsync(e => e.CourseId == course.Id))
            .OrderBy(e => e.CreationTime)
            .Take(maxExercises)
            .ToList();
        if (!exercises.Any())
        {
            return (0, 0, 0);
        }

        var exerciseIds = exercises.Select(e => e.Id).ToList();
        var existingMaps = await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId));

        var manualMapped = existingMaps
            .Where(m => m.Source == KpMapSource.Manual)
            .Select(m => m.ExerciseId)
            .ToHashSet();

        var llmMapped = existingMaps
            .Where(m => m.Source == KpMapSource.Llm)
            .Select(m => m.ExerciseId)
            .ToHashSet();

        var existingPairs = existingMaps
            .Select(m => (m.ExerciseId, m.KnowledgePointId))
            .ToHashSet();

        var processed = 0;
        var createdMappings = 0;
        var kept = 0;

        foreach (var exercise in exercises)
        {
            // 老师手工维护的映射永远不动
            if (manualMapped.Contains(exercise.Id))
            {
                continue;
            }

            // 增量模式：已经由 LLM 抽过的题跳过
            if (mode == KpGenerationMode.ExerciseIncremental && llmMapped.Contains(exercise.Id))
            {
                continue;
            }

            var extracted = await _extractor.ExtractAsync(
                course.Title, exercise.Title, exercise.QuestionContent, exercise.Options,
                kpByName.Keys.ToList());

            if (!extracted.Any())
            {
                // AI 不可用 / 抽取为空：保留该题原有关联，避免全量刷新把数据清空
                kept++;
                continue;
            }

            // 抽取成功才覆盖：清掉该题的非人工映射（Seed 占位 + 上一轮 LLM 结果），避免权重叠加
            await _mapRepository.DeleteAsync(m =>
                m.ExerciseId == exercise.Id &&
                (m.Source == KpMapSource.Seed || m.Source == KpMapSource.Llm));

            foreach (var pair in existingPairs.Where(p => p.ExerciseId == exercise.Id).ToList())
            {
                existingPairs.Remove(pair);
            }

            foreach (var item in extracted)
            {
                var (kp, _) = await ResolveKnowledgePointAsync(
                    course.Id, item.Name, kps, kpByName, exercise.ChapterId, null);

                if (existingPairs.Contains((exercise.Id, kp.Id)))
                {
                    continue;
                }

                await _mapRepository.InsertAsync(new ExerciseKnowledgePoint(_guidGenerator.Create(), exercise.Id, kp.Id)
                {
                    Weight = item.Weight,
                    Source = KpMapSource.Llm,
                }, autoSave: true);
                existingPairs.Add((exercise.Id, kp.Id));
                createdMappings++;
            }

            processed++;
        }

        _logger.LogInformation(
            "习题关联刷新：处理 {Processed}/{Total}，新增关联 {Created}，保留原关联（抽取失败） {Kept}",
            processed, exercises.Count, createdMappings, kept);

        return (processed, exercises.Count, createdMappings);
    }

    // ══════════════════════════════════════════════════════════
    //  阶段三：语义索引
    // ══════════════════════════════════════════════════════════

    /// <summary>重建课程内全部知识点的语义向量并同步 pgvector 索引，返回写入数。</summary>
    private async Task<int> RebuildIndexAsync(Guid courseId)
    {
        var kps = (await _kpRepository.GetListAsync(k => k.CourseId == courseId))
            .Where(k => k.CanonicalId == null)
            .ToList();
        if (!kps.Any())
        {
            return 0;
        }

        var embeddingResult = await _embeddingProvider.EmbedAsync(kps.Select(k => k.Name).ToList());
        if (!embeddingResult.IsAvailable)
        {
            _logger.LogWarning("语义索引重建跳过：向量服务不可用（provider=none）");
            return 0;
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
        }

        await _vectorStore.UpsertAsync(written, _currentTenant.Id);
        return written.Count;
    }

    // ══════════════════════════════════════════════════════════
    //  归一化
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 把 AI 给出的知识点名解析到课程内的标准知识点：
    /// 精确同名 → 互相包含 → 语义相似（≥0.88 归并，0.75~0.88 灰区）→ 新建。
    /// </summary>
    private async Task<(KnowledgePoint Kp, bool IsNew)> ResolveKnowledgePointAsync(
        Guid courseId,
        string rawName,
        List<KnowledgePoint> kps,
        Dictionary<string, KnowledgePoint> kpByName,
        Guid? chapterId,
        string? description)
    {
        var name = Normalize(rawName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "未命名知识点";
        }

        // 1) 精确同名
        if (kpByName.TryGetValue(name, out var exact))
        {
            return (exact, false);
        }

        // 2) 互相包含（短名 ≥ 2 字）
        var contained = kps.FirstOrDefault(k =>
        {
            var other = k.Name.Trim();
            var shorter = Math.Min(other.Length, name.Length);
            return shorter >= 2 &&
                   (other.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(other, StringComparison.OrdinalIgnoreCase));
        });
        if (contained != null)
        {
            kpByName[name] = contained;
            return (contained, false);
        }

        // 3) 语义相似度（有向量时优先，无向量回退字符近似）
        var (bestKp, bestSimilarity) = await FindMostSimilarAsync(name, kps);

        var autoMerge = _configuration.GetValue("LearningAnalytics:KpAutoMergeThreshold", 0.88);
        var review = _configuration.GetValue("LearningAnalytics:KpReviewThreshold", 0.75);

        if (bestKp != null && bestSimilarity >= autoMerge)
        {
            // 语义几乎一致 → 直接归并到已有知识点，避免同义造词
            kpByName[name] = bestKp;
            return (bestKp, false);
        }

        var status = bestSimilarity >= review
            ? KnowledgePointStatus.PendingReview
            : KnowledgePointStatus.Confirmed;

        var created = new KnowledgePoint(_guidGenerator.Create(), courseId, name)
        {
            ChapterId = chapterId,
            Description = description,
            SyllabusOrder = 500,
            Source = KpMapSource.Llm,
            Status = status,
        };

        // 顺便给新知识点生成向量，方便后续合并建议
        var embedding = await _embeddingProvider.EmbedOneAsync(name);
        if (embedding is { Length: > 0 })
        {
            created.Embedding = embedding;
        }

        await _kpRepository.InsertAsync(created, autoSave: true);

        // 保持 pgvector 索引同步（不可用时静默跳过）
        if (created.Embedding is { Length: > 0 } createdVector)
        {
            await _vectorStore.UpsertAsync(new[] { (created.Id, createdVector) }, _currentTenant.Id);
        }

        kps.Add(created);
        kpByName[name] = created;
        return (created, true);
    }

    /// <summary>给课程内缺向量的知识点批量补向量（向量服务不可用时静默跳过）。</summary>
    private async Task EnsureEmbeddingsAsync(List<KnowledgePoint> kps)
    {
        var missing = kps.Where(k => k.Embedding is not { Length: > 0 }).ToList();
        if (!missing.Any())
        {
            return;
        }

        var result = await _embeddingProvider.EmbedAsync(missing.Select(k => k.Name).ToList());
        if (!result.IsAvailable)
        {
            return;
        }

        var written = new List<(Guid Id, float[] Vector)>();
        for (var i = 0; i < missing.Count; i++)
        {
            var vector = result.Vectors[i];
            if (vector is not { Length: > 0 })
            {
                continue;
            }

            missing[i].Embedding = vector;
            await _kpRepository.UpdateAsync(missing[i], autoSave: true);
            written.Add((missing[i].Id, vector));
        }

        // 同步到 pgvector 索引（不可用时静默跳过）
        await _vectorStore.UpsertAsync(written, _currentTenant.Id);
    }

    /// <summary>找出与给定名称最相似的知识点（余弦相似度优先，回退字符 Jaccard）。</summary>
    private async Task<(KnowledgePoint? Kp, double Similarity)> FindMostSimilarAsync(
        string name,
        List<KnowledgePoint> kps)
    {
        if (!kps.Any())
        {
            return (null, 0);
        }

        var withVector = kps.Where(k => k.Embedding is { Length: > 0 }).ToList();
        if (withVector.Any())
        {
            var probe = await _embeddingProvider.EmbedOneAsync(name);
            if (probe is { Length: > 0 })
            {
                KnowledgePoint? best = null;
                var bestScore = 0.0;
                foreach (var kp in withVector)
                {
                    if (kp.Embedding!.Length != probe.Length)
                    {
                        continue;
                    }

                    double dot = 0, na = 0, nb = 0;
                    for (var i = 0; i < probe.Length; i++)
                    {
                        dot += (double)probe[i] * kp.Embedding[i];
                        na += (double)probe[i] * probe[i];
                        nb += (double)kp.Embedding[i] * kp.Embedding[i];
                    }

                    if (na <= 0 || nb <= 0)
                    {
                        continue;
                    }

                    var score = dot / (Math.Sqrt(na) * Math.Sqrt(nb));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = kp;
                    }
                }

                if (best != null)
                {
                    return (best, bestScore);
                }
            }
        }

        KnowledgePoint? fallback = null;
        var fallbackScore = 0.0;
        foreach (var kp in kps)
        {
            var score = CharJaccard(kp.Name.Trim(), name);
            if (score > fallbackScore)
            {
                fallbackScore = score;
                fallback = kp;
            }
        }

        return (fallback, fallbackScore);
    }

    private static string Normalize(string raw) =>
        (raw ?? string.Empty).Trim().Trim('「', '」', '"', '\'', '“', '”').Replace(" ", string.Empty);

    /// <summary>字符集合 Jaccard 相似度（中文短语近似）。</summary>
    internal static double CharJaccard(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
        var setA = a.ToHashSet();
        var setB = b.ToHashSet();
        var inter = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0 : (double)inter / union;
    }
}
