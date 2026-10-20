using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.KnowledgeGraph;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 知识点初始化与「习题 → 知识点」映射。
///
/// 阶段 B 采用可解释的规则初始化（不调用 LLM）：
/// 1) 优先按课程「叶子章节」生成知识点；
/// 2) 没有章节时，按知识图谱节点（KnowledgeNode）生成；
/// 3) 再退化到课程知识资源（KnowledgeResource）。
/// 习题按 <c>Exercise.ChapterId</c> / <c>ChapterExercise</c> 映射到同章节知识点；
/// 无法归属的习题落到「课程综合」兜底知识点，保证分析链路不缺数据。
///
/// 后续可接入 LLM 抽取 + 向量归一化（接口预留 <see cref="KpMapSource.Llm"/>）。
/// </summary>
public class KnowledgePointIndexer : ITransientDependency
{
    public const string CatchAllName = "课程综合";

    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<ExerciseKnowledgePoint> _mapRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource> _resourceRepository;
    private readonly IRepository<KnowledgeNode, Guid> _nodeRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public KnowledgePointIndexer(
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<ExerciseKnowledgePoint> mapRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource> resourceRepository,
        IRepository<KnowledgeNode, Guid> nodeRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _kpRepository = kpRepository;
        _mapRepository = mapRepository;
        _chapterRepository = chapterRepository;
        _resourceRepository = resourceRepository;
        _nodeRepository = nodeRepository;
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    /// <summary>确保课程知识点已建立，并同步习题映射，返回课程知识点。</summary>
    public async Task<List<KnowledgePoint>> EnsureAsync(Guid courseId)
    {
        var kps = await _kpRepository.GetListAsync(x => x.CourseId == courseId);
        if (!kps.Any())
        {
            kps = await SeedAsync(courseId);
        }

        await SyncExerciseMappingsAsync(courseId, kps);
        return await _kpRepository.GetListAsync(x => x.CourseId == courseId);
    }

    private async Task<List<KnowledgePoint>> SeedAsync(Guid courseId)
    {
        var created = new List<KnowledgePoint>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var chapters = await _chapterRepository.GetListAsync(x => x.CourseId == courseId);
        if (chapters.Any())
        {
            var parentIds = chapters.Where(c => c.ParentId.HasValue).Select(c => c.ParentId!.Value).ToHashSet();
            var leaves = chapters.Where(c => !parentIds.Contains(c.Id)).OrderBy(c => c.SortOrder).ToList();
            if (!leaves.Any())
            {
                leaves = chapters.OrderBy(c => c.SortOrder).ToList();
            }

            var order = 0;
            foreach (var chapter in leaves)
            {
                // 同课程存在同名章节时只建一个知识点，避免出现重复行
                if (!usedNames.Add(chapter.Title.Trim()))
                {
                    continue;
                }

                order++;
                var kp = new KnowledgePoint(_guidGenerator.Create(), courseId, chapter.Title)
                {
                    ChapterId = chapter.Id,
                    SyllabusOrder = order,
                    Source = KpMapSource.Seed,
                    Status = KnowledgePointStatus.Confirmed,
                };
                created.Add(await _kpRepository.InsertAsync(kp, autoSave: true));
            }
        }

        if (!created.Any())
        {
            var nodes = await _nodeRepository.GetListAsync(x => x.CourseId == courseId);
            var order = 0;
            foreach (var node in nodes)
            {
                if (!usedNames.Add(node.Name.Trim()))
                {
                    continue;
                }

                order++;
                var kp = new KnowledgePoint(_guidGenerator.Create(), courseId, node.Name)
                {
                    Description = node.Description,
                    SyllabusOrder = order,
                    Embedding = node.Embedding,
                    Source = KpMapSource.Seed,
                    Status = KnowledgePointStatus.Confirmed,
                };
                created.Add(await _kpRepository.InsertAsync(kp, autoSave: true));
            }
        }

        if (!created.Any())
        {
            var resources = await _resourceRepository.GetListAsync(x => x.CourseId == courseId);
            var order = 0;
            foreach (var resource in resources.Where(r => r.ParentId == null).OrderBy(r => r.SortOrder))
            {
                if (!usedNames.Add(resource.Name.Trim()))
                {
                    continue;
                }

                order++;
                var kp = new KnowledgePoint(_guidGenerator.Create(), courseId, resource.Name)
                {
                    ChapterId = resource.ChapterId,
                    Description = resource.Description,
                    SyllabusOrder = order,
                    Embedding = resource.Embedding,
                    Source = KpMapSource.Seed,
                    Status = KnowledgePointStatus.Confirmed,
                };
                created.Add(await _kpRepository.InsertAsync(kp, autoSave: true));
            }
        }

        return created;
    }

    /// <summary>把课程内习题映射到知识点（按章节归属，权重均分）。</summary>
    public async Task SyncExerciseMappingsAsync(Guid courseId, List<KnowledgePoint>? kps = null)
    {
        kps ??= await _kpRepository.GetListAsync(x => x.CourseId == courseId);
        if (!kps.Any())
        {
            return;
        }

        var exercises = await _exerciseRepository.GetListAsync(x => x.CourseId == courseId);
        if (!exercises.Any())
        {
            return;
        }

        var existingMaps = await _mapRepository.GetListAsync(m =>
            exercises.Select(e => e.Id).Contains(m.ExerciseId));
        var mappedExerciseIds = existingMaps.Select(m => m.ExerciseId).ToHashSet();

        var chapterIds = kps.Where(k => k.ChapterId.HasValue).Select(k => k.ChapterId!.Value).ToHashSet();
        var chapterExercises = chapterIds.Any()
            ? await _chapterExerciseRepository.GetListAsync(x => chapterIds.Contains(x.ChapterId))
            : new List<ChapterExercise>();

        var byChapter = kps.Where(k => k.ChapterId.HasValue)
            .GroupBy(k => k.ChapterId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        KnowledgePoint? catchAll = null;
        var pending = new List<(Guid ExerciseId, List<KnowledgePoint> Targets)>();

        // 章节树：用于把「挂在父章节 / 兄弟章节」上的习题归到最近的叶子知识点
        var courseChapters = await _chapterRepository.GetListAsync(x => x.CourseId == courseId);
        var chapterById = courseChapters.ToDictionary(c => c.Id);

        List<KnowledgePoint> ResolveForChapter(Guid chapterId)
        {
            if (byChapter.TryGetValue(chapterId, out var directKps))
            {
                return directKps;
            }

            // 向上找最近的有知识点的祖先章节
            var cursor = chapterById.GetValueOrDefault(chapterId);
            var guardUp = 0;
            while (cursor?.ParentId != null && guardUp++ < 16)
            {
                if (byChapter.TryGetValue(cursor.ParentId.Value, out var upKps))
                {
                    return upKps;
                }

                cursor = chapterById.GetValueOrDefault(cursor.ParentId.Value);
            }

            // 向下收集子孙章节里有知识点的
            var found = new List<KnowledgePoint>();
            var stack = new Stack<Guid>();
            var seen = new HashSet<Guid>();
            stack.Push(chapterId);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!seen.Add(id))
                {
                    continue;
                }

                if (byChapter.TryGetValue(id, out var childKps))
                {
                    found.AddRange(childKps);
                }

                foreach (var child in courseChapters.Where(c => c.ParentId == id))
                {
                    stack.Push(child.Id);
                }
            }

            return found;
        }

        foreach (var exercise in exercises)
        {
            if (mappedExerciseIds.Contains(exercise.Id))
            {
                continue;
            }

            var targets = new List<KnowledgePoint>();

            if (exercise.ChapterId.HasValue)
            {
                targets.AddRange(ResolveForChapter(exercise.ChapterId.Value));
            }

            if (!targets.Any())
            {
                var linked = chapterExercises.Where(x => x.ExerciseId == exercise.Id)
                    .Select(x => x.ChapterId)
                    .Distinct()
                    .ToList();
                foreach (var chapterId in linked)
                {
                    targets.AddRange(ResolveForChapter(chapterId));
                }
            }

            if (!targets.Any())
            {
                catchAll ??= await GetOrCreateCatchAllAsync(courseId);
                targets.Add(catchAll);
            }

            pending.Add((exercise.Id, targets.DistinctBy(t => t.Id).ToList()));
        }

        if (!pending.Any())
        {
            return;
        }

        foreach (var (exerciseId, targets) in pending)
        {
            var weight = targets.Count > 0 ? 1.0 / targets.Count : 1.0;
            foreach (var kp in targets)
            {
                await _mapRepository.InsertAsync(new ExerciseKnowledgePoint(_guidGenerator.Create(), exerciseId, kp.Id)
                {
                    Weight = weight,
                    Source = KpMapSource.Seed,
                }, autoSave: true);
            }
        }

        await Task.CompletedTask;
    }

    private async Task<KnowledgePoint> GetOrCreateCatchAllAsync(Guid courseId)
    {
        var list = await _kpRepository.GetListAsync(x => x.CourseId == courseId && x.Name == CatchAllName);
        var existing = list.FirstOrDefault();
        if (existing != null)
        {
            return existing;
        }

        var kp = new KnowledgePoint(_guidGenerator.Create(), courseId, CatchAllName)
        {
            SyllabusOrder = 999,
            Status = KnowledgePointStatus.Confirmed,
            Source = KpMapSource.Seed,
        };
        return await _kpRepository.InsertAsync(kp, autoSave: true);
    }

    /// <summary>通用知识点解析：精确命中 → 新建（灰区标记待确认由 LLM 阶段处理）。</summary>
    public async Task<KnowledgePoint> ResolveAsync(Guid courseId, string name, Guid? chapterId = null,
        KpMapSource source = KpMapSource.Llm)
    {
        var normalized = name.Trim();
        var list = await _kpRepository.GetListAsync(x =>
            x.CourseId == courseId && x.Name == normalized && x.CanonicalId == null);
        var exact = list.FirstOrDefault();
        if (exact != null)
        {
            return exact;
        }

        using (_currentTenant.Change(_currentTenant.Id))
        {
            var kp = new KnowledgePoint(_guidGenerator.Create(), courseId, normalized)
            {
                ChapterId = chapterId,
                SyllabusOrder = 500,
                Source = source,
                Status = KnowledgePointStatus.Confirmed,
            };
            return await _kpRepository.InsertAsync(kp, autoSave: true);
        }
    }
}
