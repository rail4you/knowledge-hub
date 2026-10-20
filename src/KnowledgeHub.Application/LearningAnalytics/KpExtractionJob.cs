using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.LearningAnalytics.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 知识点抽取任务：按课程批量让 LLM 抽取习题知识点并归一化写入映射。
///
/// 归一化策略（无向量时的可解释近似）：
/// 1) 精确同名（忽略大小写/空白）→ 复用；
/// 2) 互相包含且短名长度 ≥ 2 → 复用（如「级数收敛」⊂「级数收敛判别」）；
/// 3) 字符 Jaccard ≥ 0.75 → 新建但标记 <see cref="KnowledgePointStatus.PendingReview"/>，交给老师确认合并；
/// 4) 其余 → 新建确认态。
///
/// 老师手工维护的映射（<see cref="KpMapSource.Manual"/>）永不被覆盖。
/// </summary>
public class KpExtractionJob : ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
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
    private readonly ILogger<KpExtractionJob> _logger;

    public KpExtractionJob(
        IRepository<Course, Guid> courseRepository,
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
        ILogger<KpExtractionJob> logger)
    {
        _courseRepository = courseRepository;
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
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid courseId, bool overwrite, Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            if (!_configuration.GetValue("LearningAnalytics:KpExtractionEnabled", true))
            {
                _logger.LogInformation("知识点抽取已关闭（LearningAnalytics:KpExtractionEnabled=false）");
                return;
            }

            var course = await _courseRepository.FindAsync(courseId);
            if (course == null)
            {
                return;
            }

            var maxExercises = _configuration.GetValue("LearningAnalytics:KpExtractionMaxExercises", 200);

            var exercises = (await _exerciseRepository.GetListAsync(e => e.CourseId == courseId))
                .OrderBy(e => e.CreationTime)
                .Take(maxExercises)
                .ToList();
            if (!exercises.Any())
            {
                return;
            }

            var exerciseIds = exercises.Select(e => e.Id).ToList();
            var existingMaps = await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId));

            if (overwrite)
            {
                var llmMaps = existingMaps.Where(m => m.Source == KpMapSource.Llm).ToList();
                foreach (var map in llmMaps)
                {
                    await _mapRepository.DeleteAsync(map, autoSave: true);
                }

                existingMaps = existingMaps.Where(m => m.Source != KpMapSource.Llm).ToList();
            }

            var manualMapped = existingMaps
                .Where(m => m.Source == KpMapSource.Manual)
                .Select(m => m.ExerciseId)
                .ToHashSet();

            // 增量模式只处理「还没被 LLM 抽过」的题；
            // 章节初始化产生的 Seed 映射可以升级为 LLM 映射（抽完会替换掉）。
            var llmMapped = existingMaps
                .Where(m => m.Source == KpMapSource.Llm)
                .Select(m => m.ExerciseId)
                .ToHashSet();
            var alreadyMapped = existingMaps.Select(m => m.ExerciseId).ToHashSet();

            var kps = new List<KnowledgePoint>(
                (await _kpRepository.GetListAsync(k => k.CourseId == courseId)).Where(k => k.CanonicalId == null));
            await EnsureEmbeddingsAsync(kps);
            var kpByName = kps.ToDictionary(k => k.Name.Trim(), k => k, StringComparer.OrdinalIgnoreCase);
            var existingPairs = existingMaps
                .Select(m => (m.ExerciseId, m.KnowledgePointId))
                .ToHashSet();

            var processed = 0;
            var createdMappings = 0;

            foreach (var exercise in exercises)
            {
                // 老师手工维护的映射永远不动
                if (manualMapped.Contains(exercise.Id))
                {
                    continue;
                }

                // 增量模式：已经由 LLM 抽过的题跳过
                if (!overwrite && llmMapped.Contains(exercise.Id))
                {
                    continue;
                }

                var extracted = await _extractor.ExtractAsync(
                    course.Title, exercise.Title, exercise.QuestionContent, exercise.Options,
                    kpByName.Keys.ToList());

                if (!extracted.Any())
                {
                    continue;
                }

                // 清掉该题的非人工映射（Seed 占位 + 覆盖时的上一轮 LLM 结果），避免权重叠加
                await _mapRepository.DeleteAsync(m =>
                    m.ExerciseId == exercise.Id &&
                    (m.Source == KpMapSource.Seed || m.Source == KpMapSource.Llm));

                foreach (var pair in existingPairs.Where(p => p.ExerciseId == exercise.Id).ToList())
                {
                    existingPairs.Remove(pair);
                }

                foreach (var item in extracted)
                {
                    var kp = await ResolveKnowledgePointAsync(courseId, item.Name, kps, kpByName);
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
                "知识点抽取完成：course {CourseId}，处理习题 {Processed}/{Total}，新增映射 {Mappings}",
                courseId, processed, exercises.Count, createdMappings);

            await uow.CompleteAsync();
        }
    }

    private async Task<KnowledgePoint> ResolveKnowledgePointAsync(
        Guid courseId,
        string rawName,
        List<KnowledgePoint> kps,
        Dictionary<string, KnowledgePoint> kpByName)
    {
        var name = Normalize(rawName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "未命名知识点";
        }

        // 1) 精确同名
        if (kpByName.TryGetValue(name, out var exact))
        {
            return exact;
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
            return contained;
        }

        // 3) 语义相似度（有向量时优先，无向量回退字符近似）
        var (bestKp, bestSimilarity) = await FindMostSimilarAsync(name, kps);

        var autoMerge = _configuration.GetValue("LearningAnalytics:KpAutoMergeThreshold", 0.88);
        var review = _configuration.GetValue("LearningAnalytics:KpReviewThreshold", 0.75);

        if (bestKp != null && bestSimilarity >= autoMerge)
        {
            // 语义几乎一致 → 直接归并到已有知识点，避免同义造词
            kpByName[name] = bestKp;
            return bestKp;
        }

        var status = bestSimilarity >= review
            ? KnowledgePointStatus.PendingReview
            : KnowledgePointStatus.Confirmed;

        var created = new KnowledgePoint(_guidGenerator.Create(), courseId, name)
        {
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
        return created;
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
