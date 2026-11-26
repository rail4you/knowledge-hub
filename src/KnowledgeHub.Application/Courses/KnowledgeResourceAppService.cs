using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程知识点（内容主干）管理。
/// 除基础 CRUD 外，提供「树查询 / 移动 / 同级重排 / 物化路径重算」，
/// 让知识点树可以像章节树一样被拖拽维护（对齐 kg-edu 的 subject → unit → cell）。
/// </summary>
[IgnoreAntiforgeryToken]
public class KnowledgeResourceAppService : KnowledgeHubAppService, IKnowledgeResourceAppService
{
    /// <summary>物化路径最大层级，防止异常数据造成无限递归。</summary>
    private const int MaxDepth = 32;

    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IDataFilter _dataFilter;
    /// <summary>xlsx 知识点导入（解析逻辑在导入器里，AppService 只做权限与入口）</summary>
    private readonly KnowledgeResourceExcelImporter _excelImporter;

    public KnowledgeResourceAppService(
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<CourseResource, Guid> courseResourceRepository,
        IDataFilter dataFilter,
        KnowledgeResourceExcelImporter excelImporter)
    {
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _chapterRepository = chapterRepository;
        _courseResourceRepository = courseResourceRepository;
        _dataFilter = dataFilter;
        _excelImporter = excelImporter;
    }

    public async Task<List<KnowledgeResourceDto>> GetByCourseAsync(Guid courseId)
    {
        var query = await _knowledgeResourceRepository.GetQueryableAsync();
        var resources = query
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.SortOrder)
            .ToList();

        return resources.Select(MapToDto).ToList();
    }

    public async Task<List<KnowledgeResourceDto>> GetByChapterAsync(Guid chapterId)
    {
        var query = await _knowledgeResourceRepository.GetQueryableAsync();
        var resources = query
            .Where(x => x.ChapterId == chapterId)
            .OrderBy(x => x.SortOrder)
            .ToList();

        return resources.Select(MapToDto).ToList();
    }

    public async Task<List<KnowledgeResourceDto>> GetTreeAsync(Guid courseId)
    {
        var items = await GetCourseItemsAsync(courseId);
        return BuildTree(items);
    }

    public async Task<RelatedCoursesResultDto> GetRelatedCoursesAsync(Guid knowledgeResourceId)
    {
        var resource = await _knowledgeResourceRepository.GetAsync(knowledgeResourceId);

        var chapterQuery = await _chapterRepository.GetQueryableAsync();

        var relatedChapters = chapterQuery
            .Where(x => x.Id == resource.ChapterId)
            .Select(x => new { ChapterId = x.Id, ChapterTitle = x.Title, x.CourseId })
            .ToList();

        return new RelatedCoursesResultDto
        {
            KnowledgeResourceId = knowledgeResourceId,
            Name = resource.Name,
            Courses = relatedChapters
                .GroupBy(x => x.CourseId)
                .Select(g => new RelatedCourseInfoDto
                {
                    CourseId = g.Key,
                    Chapters = g.Select(c => new RelatedChapterInfoDto
                    {
                        ChapterId = c.ChapterId,
                        ChapterTitle = c.ChapterTitle ?? "未知章节"
                    }).ToList()
                }).ToList()
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<KnowledgeResourceDto> CreateAsync(CreateUpdateKnowledgeResourceDto input)
    {
        await ValidateResourceLinkAsync(input.CourseId, input.ChapterId, input.ResourceId);
        await ValidateParentAsync(input.CourseId, input.ParentId, null);

        if (input.ResourceId.HasValue && input.ResourceId.Value != Guid.Empty)
        {
            var duplicateQuery = await _knowledgeResourceRepository.GetQueryableAsync();
            var exists = await AsyncExecuter.AnyAsync(duplicateQuery.Where(x =>
                x.ChapterId == input.ChapterId &&
                x.ResourceId == input.ResourceId.Value));
            if (exists)
            {
                throw new UserFriendlyException("该资源已关联到当前章节。");
            }
        }

        var resource = new KnowledgeResource(
            GuidGenerator.Create(),
            input.CourseId,
            input.Name)
        {
            ChapterId = input.ChapterId,
            Description = input.Description,
            Content = input.Content,
            ImportanceLevel = input.ImportanceLevel,
            Difficulty = input.Difficulty,
            SortOrder = input.SortOrder,
            Tags = input.Tags,
            ParentId = input.ParentId,
            ResourceId = input.ResourceId,
            NodeType = input.NodeType
        };

        await _knowledgeResourceRepository.InsertAsync(resource);

        // 新建后把该课程的知识点路径补齐（新节点默认排在同级末尾）
        await RebuildPathsAsync(input.CourseId);

        return MapToDto(resource);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<KnowledgeResourceDto> UpdateAsync(Guid id, CreateUpdateKnowledgeResourceDto input)
    {
        await ValidateResourceLinkAsync(input.CourseId, input.ChapterId, input.ResourceId);

        var resource = await _knowledgeResourceRepository.GetAsync(id);

        if (input.ParentId != resource.ParentId)
        {
            await ValidateParentAsync(input.CourseId, input.ParentId, id, resource.CourseId);
        }

        resource.Name = input.Name;
        resource.ChapterId = input.ChapterId;
        resource.Description = input.Description;
        resource.Content = input.Content;
        resource.ImportanceLevel = input.ImportanceLevel;
        resource.Difficulty = input.Difficulty;
        resource.SortOrder = input.SortOrder;
        resource.Tags = input.Tags;
        resource.ParentId = input.ParentId;
        resource.ResourceId = input.ResourceId;
        resource.NodeType = input.NodeType;

        await _knowledgeResourceRepository.UpdateAsync(resource);
        await RebuildPathsAsync(resource.CourseId);
        return MapToDto(resource);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        var resource = await _knowledgeResourceRepository.GetAsync(id);
        var items = await GetCourseItemsAsync(resource.CourseId);

        // 子节点上移一级而不是级联删除：知识点下往往挂着习题/视频关联，
        // 误删父节点时保留子节点比一起删掉更安全（与章节的"整章删除"语义区分开）。
        var children = items.Where(x => x.ParentId == id).ToList();
        foreach (var child in children)
        {
            child.ParentId = resource.ParentId;
            await _knowledgeResourceRepository.UpdateAsync(child);
        }

        await _knowledgeResourceRepository.DeleteAsync(id);
        await RebuildPathsAsync(resource.CourseId);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<KnowledgeResourceDto> MoveAsync(Guid id, MoveKnowledgeResourceInput input)
    {
        var node = await _knowledgeResourceRepository.GetAsync(id);
        var items = await GetCourseItemsAsync(node.CourseId);

        if (input.ParentId.HasValue)
        {
            await ValidateParentAsync(node.CourseId, input.ParentId, id, node.CourseId);
        }

        node.ParentId = input.ParentId;

        // 同级重排：把同父级的其它节点按现有顺序取出，把本节点插到目标位置
        var siblings = items
            .Where(x => x.ParentId == input.ParentId && x.Id != id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        var index = input.Index ?? siblings.Count;
        index = Math.Clamp(index, 0, siblings.Count);
        siblings.Insert(index, node);

        for (var i = 0; i < siblings.Count; i++)
        {
            siblings[i].SortOrder = i;
            await _knowledgeResourceRepository.UpdateAsync(siblings[i]);
        }

        await RebuildPathsAsync(node.CourseId, items);
        return MapToDto(node);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<List<KnowledgeResourceDto>> BatchReorderAsync(BatchReorderKnowledgeResourcesInput input)
    {
        if (input.OrderedIds == null || input.OrderedIds.Count == 0)
        {
            return await GetTreeAsync(input.CourseId);
        }

        var items = await GetCourseItemsAsync(input.CourseId);
        var byId = items.ToDictionary(x => x.Id);

        var missing = input.OrderedIds.Where(x => !byId.ContainsKey(x)).ToList();
        if (missing.Any())
        {
            throw new UserFriendlyException("存在不属于当前课程的知识点，无法重排。");
        }

        var invalid = input.OrderedIds.Where(x => byId[x].ParentId != input.ParentId).ToList();
        if (invalid.Any())
        {
            throw new UserFriendlyException("只能对同一层级的知识点进行重排。");
        }

        for (var i = 0; i < input.OrderedIds.Count; i++)
        {
            var node = byId[input.OrderedIds[i]];
            node.SortOrder = i;
            await _knowledgeResourceRepository.UpdateAsync(node);
        }

        await RebuildPathsAsync(input.CourseId, items);
        return BuildTree(items);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<int> RegenerateSortPathsAsync(Guid courseId)
    {
        var items = await GetCourseItemsAsync(courseId);
        var changed = RebuildPaths(items);
        foreach (var item in changed)
        {
            await _knowledgeResourceRepository.UpdateAsync(item);
        }

        return changed.Count;
    }

    // =====================================================================
    //  Excel 导入（只支持 xlsx）
    // =====================================================================

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<KnowledgeResourceImportResultDto> ImportFromExcelAsync(Guid courseId, Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("请选择要导入的 .xlsx 文件。");
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            throw new UserFriendlyException("文件超过 20MB，请拆分后分批导入。");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        return await _excelImporter.ImportAsync(courseId, stream, file.FileName);
    }

    public async Task<Volo.Abp.Content.IRemoteStreamContent> GetImportTemplateAsync()
    {
        var bytes = _excelImporter.BuildTemplate();
        return new Volo.Abp.Content.RemoteStreamContent(
            new MemoryStream(bytes),
            "知识点导入模板.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // =====================================================================
    //  物化路径与树
    // =====================================================================

    private async Task<List<KnowledgeResource>> GetCourseItemsAsync(Guid courseId)
    {
        var query = await _knowledgeResourceRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(query.Where(x => x.CourseId == courseId));
    }

    private async Task RebuildPathsAsync(Guid courseId, List<KnowledgeResource>? items = null)
    {
        // 关键：RebuildPaths 会重新查库（GetQueryableAsync 不走变更追踪）。
        // 同一 UoW 内刚插入的节点若未落库，这里查不到它 → 新节点的 SortPath 会一直是 null。
        if (CurrentUnitOfWork is not null)
        {
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        items ??= await GetCourseItemsAsync(courseId);
        var changed = RebuildPaths(items);
        foreach (var item in changed)
        {
            await _knowledgeResourceRepository.UpdateAsync(item);
        }
    }

    /// <summary>
    /// 按「父级 + SortOrder」重算所有节点的 SortOrder（同级归一化）与 SortPath。
    /// 只用一次内存分组，不产生 N 次查询；返回真正发生变化的节点。
    /// </summary>
    private static List<KnowledgeResource> RebuildPaths(List<KnowledgeResource> items)
    {
        var changed = new List<KnowledgeResource>();
        if (items.Count == 0)
        {
            return changed;
        }

        var byParent = items
            .GroupBy(x => x.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList());

        var visited = new HashSet<Guid>();

        void Walk(Guid parentKey, string parentPath, int depth)
        {
            if (depth > MaxDepth || !byParent.TryGetValue(parentKey, out var siblings))
            {
                return;
            }

            for (var i = 0; i < siblings.Count; i++)
            {
                var node = siblings[i];
                if (!visited.Add(node.Id))
                {
                    continue; // 环路保护：脏数据（自引用/互指）只处理一次
                }

                var segment = (i + 1).ToString("D4");
                var path = string.IsNullOrEmpty(parentPath) ? segment : parentPath + "." + segment;

                var dirty = node.SortPath != path;
                if (dirty)
                {
                    node.SortPath = path;
                    changed.Add(node);
                }

                if (node.SortOrder != i)
                {
                    node.SortOrder = i;
                    if (!dirty)
                    {
                        changed.Add(node);
                    }
                }

                Walk(node.Id, path, depth + 1);
            }
        }

        Walk(Guid.Empty, string.Empty, 0);

        // 环路残留（父级指向自己或互相指）的节点：兜底给一个平铺路径，避免永远是 null
        foreach (var orphan in items.Where(x => !visited.Contains(x.Id)))
        {
            var fallback = "9999." + orphan.Id.ToString("N")[..8];
            if (orphan.SortPath != fallback)
            {
                orphan.SortPath = fallback;
                changed.Add(orphan);
            }
        }

        return changed;
    }

    /// <summary>按 ParentId 建树（避免前端自己拼树，也避免 N+1）。</summary>
    private static List<KnowledgeResourceDto> BuildTree(List<KnowledgeResource> items)
    {
        var byParent = items
            .GroupBy(x => x.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.SortPath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.Name)
                .ToList());

        var visited = new HashSet<Guid>();

        List<KnowledgeResourceDto> Walk(Guid parentKey, int depth)
        {
            if (depth > MaxDepth || !byParent.TryGetValue(parentKey, out var siblings))
            {
                return new List<KnowledgeResourceDto>();
            }

            var result = new List<KnowledgeResourceDto>(siblings.Count);
            foreach (var node in siblings)
            {
                if (!visited.Add(node.Id))
                {
                    continue;
                }

                var dto = MapToDto(node);
                dto.Children = Walk(node.Id, depth + 1);
                result.Add(dto);
            }

            return result;
        }

        return Walk(Guid.Empty, 0);
    }

    /// <summary>校验目标父级：必须存在、同课程、且不能是自己的子孙（防环路）。</summary>
    private async Task ValidateParentAsync(Guid courseId, Guid? parentId, Guid? movingId, Guid? nodeCourseId = null)
    {
        if (!parentId.HasValue || parentId.Value == Guid.Empty)
        {
            return;
        }

        if (movingId.HasValue && parentId.Value == movingId.Value)
        {
            throw new UserFriendlyException("不能把知识点挂到它自己下面。");
        }

        var parent = await _knowledgeResourceRepository.FindAsync(parentId.Value);
        if (parent == null)
        {
            throw new UserFriendlyException("上级知识点不存在。");
        }

        if (parent.CourseId != courseId)
        {
            throw new UserFriendlyException("不能跨课程移动知识点。");
        }

        if (!movingId.HasValue)
        {
            return;
        }

        // 环路检测：沿 parent 向上找，遇到 movingId 说明目标在自己的子树里
        var items = await GetCourseItemsAsync(courseId);
        var byId = items.ToDictionary(x => x.Id);
        var cursor = parent;
        var guard = 0;
        while (cursor != null && guard++ <= MaxDepth)
        {
            if (cursor.Id == movingId.Value)
            {
                throw new UserFriendlyException("不能把知识点移动到它自己的子节点下。");
            }

            cursor = cursor.ParentId.HasValue && byId.TryGetValue(cursor.ParentId.Value, out var next) ? next : null;
        }
    }

    /// <summary>
    /// 章节资源必须来自课程资源池：章节属于该课程，且资源已通过 CourseResource 关联到该课程。
    /// 这样可以避免「章节里挂了资源、课程资源池里却没有」的脏数据。
    /// </summary>
    private async Task ValidateResourceLinkAsync(Guid courseId, Guid? chapterId, Guid? resourceId)
    {
        if (!resourceId.HasValue || resourceId.Value == Guid.Empty)
        {
            return;
        }

        if (chapterId.HasValue && chapterId.Value != Guid.Empty)
        {
            var chapter = await _chapterRepository.FindAsync(chapterId.Value);
            if (chapter == null)
            {
                throw new UserFriendlyException("章节不存在。");
            }
            if (chapter.CourseId != courseId)
            {
                throw new UserFriendlyException("章节不属于当前课程。");
            }
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var linkQuery = await _courseResourceRepository.GetQueryableAsync();
            var linked = await AsyncExecuter.AnyAsync(linkQuery.Where(x =>
                x.CourseId == courseId && x.ResourceId == resourceId.Value));
            if (!linked)
            {
                throw new UserFriendlyException("该资源尚未关联到当前课程，请先在「课程资源」中关联。");
            }
        }
    }

    private static KnowledgeResourceDto MapToDto(KnowledgeResource resource)
    {
        return new KnowledgeResourceDto
        {
            Id = resource.Id,
            CourseId = resource.CourseId,
            ChapterId = resource.ChapterId,
            Name = resource.Name,
            Description = resource.Description,
            Content = resource.Content,
            ImportanceLevel = resource.ImportanceLevel,
            Difficulty = resource.Difficulty,
            SortOrder = resource.SortOrder,
            Tags = resource.Tags,
            ParentId = resource.ParentId,
            ResourceId = resource.ResourceId,
            NodeType = resource.NodeType,
            SortPath = resource.SortPath,
            IsLeafType = resource.IsLeafType,
            CreationTime = resource.CreationTime,
            CreatorId = resource.CreatorId,
            LastModificationTime = resource.LastModificationTime,
            LastModifierId = resource.LastModifierId
        };
    }
}
