using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using KnowledgeHub.Courses;
using KnowledgeHub.Resources;
using Volo.Abp.Data;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

[IgnoreAntiforgeryToken]
public class ChapterAppService : ApplicationService, IChapterAppService
{
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _resourceVersionRepository;
    /// <summary>课程仓储：取课程归属学校与所属资源库（共建协作判定）。</summary>
    private readonly IRepository<Course, Guid> _courseRepository;
    /// <summary>学校档案仓储：创作者所属学校名快照。</summary>
    private readonly KnowledgeHub.Schools.ISchoolRepository _schoolRepository;
    /// <summary>多校共建内容协作权限判定。</summary>
    private readonly KnowledgeHub.ResourceLibraries.IContentCollaborationPolicy _collaborationPolicy;

    public ChapterAppService(
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> resourceVersionRepository,
        IRepository<Course, Guid> courseRepository,
        KnowledgeHub.Schools.ISchoolRepository schoolRepository,
        KnowledgeHub.ResourceLibraries.IContentCollaborationPolicy collaborationPolicy)
    {
        _chapterRepository = chapterRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _resourceRepository = resourceRepository;
        _resourceVersionRepository = resourceVersionRepository;
        _courseRepository = courseRepository;
        _schoolRepository = schoolRepository;
        _collaborationPolicy = collaborationPolicy;
    }

    public async Task<ChapterDto> GetAsync(Guid id)
    {
        var chapter = await _chapterRepository.GetAsync(id);

        // 关键修复：与 UpdateAsync 同源问题。GetAsync 只加载实体本身，
        // 不填充 Children / KnowledgeResources；mapper 迭代时会 NRE。
        // 见 ChapterAppService.UpdateAsync 与 GetChaptersByCourseAsync:107-111。
        chapter.Children ??= new List<Chapter>();
        chapter.KnowledgeResources ??= new List<KnowledgeResource>();

        var dto = ObjectMapper.Map<Chapter, ChapterDto>(chapter);
        await AttachCanEditAsync(new List<ChapterDto> { dto }, chapter.CourseId);
        return dto;
    }

    public async Task<PagedResultDto<ChapterDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        // 关键修复：同上，列表里的每个 chapter 都做兜底。
        foreach (var chapter in chapters)
        {
            chapter.Children ??= new List<Chapter>();
            chapter.KnowledgeResources ??= new List<KnowledgeResource>();
        }

        return new PagedResultDto<ChapterDto>(
            query.Count(),
            ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(chapters)
        );
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterDto> CreateAsync(CreateUpdateChapterDto input)
    {
        var chapter = new Chapter(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title,
            input.SortOrder,
            input.ParentId
        );
        chapter.Description = input.Description;

        // 协作创作溯源：多校共建课程里，章节可能由不同学校的老师创建
        await StampCreatorAsync(chapter);

        await _chapterRepository.InsertAsync(chapter);
        var created = ObjectMapper.Map<Chapter, ChapterDto>(chapter);
        created.CanEdit = true;
        return created;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto input)
    {
        var chapter = await _chapterRepository.GetAsync(id);
        await EnsureCanEditAsync(chapter);

        chapter.LastEditorId = CurrentUser.Id;
        chapter.LastEditorName = CurrentUser.Name ?? CurrentUser.UserName;
        chapter.Title = input.Title;
        chapter.Description = input.Description;
        chapter.SortOrder = input.SortOrder;
        chapter.SetParent(input.ParentId);

        await _chapterRepository.UpdateAsync(chapter);

        // 关键修复：_chapterRepository.GetAsync(id) 只加载 Chapter 实体本身，
        // 不会填充导航属性 Children / KnowledgeResources。这两个集合保持为 null。
        // 接着下面 ObjectMapper.Map<Chapter, ChapterDto> 走 Mapperly 生成的 mapper，
        // mapper 会迭代 source.Children / source.KnowledgeResources（g.cs:38
        // MapToListOfChapterDto），直接抛 NullReferenceException → 500。
        // 与 GetChaptersByCourseAsync:107-111 同样的防御：在 mapper 之前把两个
        // 集合兜底为 new List<>()，mapper 不会去查数据库，没有性能损失。
        chapter.Children ??= new List<Chapter>();
        chapter.KnowledgeResources ??= new List<KnowledgeResource>();

        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        var target = await _chapterRepository.GetAsync(id);
        await EnsureCanEditAsync(target);

        // Recursively collect all descendant IDs before deleting
        var query = await _chapterRepository.GetQueryableAsync();
        var allChapters = query.ToList();

        var idsToDelete = new List<Guid> { id };
        CollectDescendants(id, allChapters, idsToDelete);

        // 删章节前先处理它下面的「知识点资源」。
        //
        // <para>为什么必须先处理：<c>KnowledgeResource</c> 是软删除实体（<c>ISoftDelete</c>），
        // 软删只把 <c>IsDeleted</c> 置 true、行还在；而外键
        /// <c>FK_AppKnowledgeResources_AppChapters_ChapterId</c> 是 <c>NO ACTION</c>，
        // 只要章节下还留着任何一行知识点资源（哪怕已被软删），删除章节就会触发
        /// <c>23503 update or delete on table "AppChapters" violates foreign key constraint</c> → 接口 500。
        // </para>
        //
        // <para>处理方式：把知识点资源软删掉，并把 <c>ChapterId</c> 置空（NULL 不参与外键校验）。
        /// 语义上也应当如此：章节没了，它下面的知识点内容不该继续挂在课程里当孤儿。</para>
        await DetachKnowledgeResourcesAsync(idsToDelete);

        // Delete descendants first (bottom-up), then the target
        foreach (var childId in idsToDelete.Skip(1).AsEnumerable().Reverse())
        {
            await _chapterRepository.DeleteAsync(childId);
        }
        await _chapterRepository.DeleteAsync(id);
    }

    /// <summary>
    /// 软删指定章节下的知识点资源，并把 <c>ChapterId</c> 置空。
    ///
    /// <para>已软删的行同样要处理 —— 外键 <c>NO ACTION</c> 不区分 <c>IsDeleted</c>，
    /// 残留行会挡住章节删除（这里临时关掉软删过滤器才能把已软删的行查出来）。</para>
    /// </summary>
    private async Task DetachKnowledgeResourcesAsync(IReadOnlyCollection<Guid> chapterIds)
    {
        if (chapterIds.Count == 0)
        {
            return;
        }

        using (DataFilter.Disable<ISoftDelete>())
        {
            var query = await _knowledgeResourceRepository.GetQueryableAsync();
            var list = await AsyncExecuter.ToListAsync(
                query.Where(x => x.ChapterId.HasValue && chapterIds.Contains(x.ChapterId.Value)));

            foreach (var kr in list)
            {
                kr.ChapterId = null;
                if (kr.IsDeleted)
                {
                    await _knowledgeResourceRepository.UpdateAsync(kr, autoSave: true);
                }
                else
                {
                    await _knowledgeResourceRepository.DeleteAsync(kr, autoSave: true);
                }
            }
        }
    }

    /// <summary>
    /// 批量计算 CanEdit：同一课程下的章节共用一套判定上下文（课程归属租户 + 所属资源库），
    /// 避免逐章查库。同时回填创建者学校名（老数据缺失时按 CreatorTenantId 补）。
    /// </summary>
    private async Task AttachCanEditAsync(List<ChapterDto> dtos, Guid courseId)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        Course? course;
        using (DataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(courseId);
        }

        var schoolName = string.Empty;
        if (course?.TenantId is Guid ownerTenantId)
        {
            var school = await _schoolRepository.FindByTenantIdAsync(ownerTenantId);
            schoolName = school?.Name ?? string.Empty;
        }

        var canEdit = await _collaborationPolicy.CanEditContentAsync(
            CurrentUser.Id, course?.TenantId, course?.LibraryId);

        foreach (var dto in dtos)
        {
            dto.CanEdit = canEdit;
            if (string.IsNullOrWhiteSpace(dto.CreatorTenantName) && !string.IsNullOrWhiteSpace(schoolName))
            {
                dto.CreatorTenantName = schoolName;
            }
        }
    }

    /// <summary>写入创作者快照（姓名 + 所属学校名）。</summary>
    private async Task StampCreatorAsync(Chapter chapter)
    {
        chapter.CreatorId = CurrentUser.Id;
        chapter.CreatorName = CurrentUser.Name ?? CurrentUser.UserName;
        chapter.CreatorTenantId = CurrentTenant.Id;

        if (CurrentTenant.Id.HasValue)
        {
            var school = await _schoolRepository.FindByTenantIdAsync(CurrentTenant.Id.Value);
            chapter.CreatorTenantName = !string.IsNullOrWhiteSpace(school?.Name)
                ? school!.Name
                : CurrentTenant.Name;
        }
    }

    /// <summary>协作规则校验：创建者本人 / 本校院校管理员 / 共建库主建单位的院校管理员可改。</summary>
    private async Task EnsureCanEditAsync(Chapter chapter)
    {
        Course? course = null;
        using (DataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(chapter.CourseId);
        }

        var canEdit = await _collaborationPolicy.CanEditContentAsync(
            chapter.CreatorId,
            course?.TenantId,
            course?.LibraryId);

        if (!canEdit)
        {
            throw new Volo.Abp.UserFriendlyException("只能修改自己创建的章节，或由本校 / 主建单位院校管理员操作。");
        }
    }

    private void CollectDescendants(Guid parentId, List<Chapter> allChapters, List<Guid> result)
    {
        var children = allChapters.Where(c => c.ParentId == parentId).ToList();
        foreach (var child in children)
        {
            result.Add(child.Id);
            CollectDescendants(child.Id, allChapters, result);
        }
    }

    public async Task<List<ChapterDto>> GetChaptersByCourseAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Where(c => c.CourseId == courseId).OrderBy(c => c.SortOrder).ToList();

        // Ensure navigation properties are not null before mapping
        foreach (var chapter in chapters)
        {
            chapter.Children ??= new List<Chapter>();
            chapter.KnowledgeResources ??= new List<KnowledgeResource>();
        }

        var dtos = ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(chapters);
        await AttachCanEditAsync(dtos, courseId);
        return dtos;
    }

    public async Task<List<ChapterDto>> GetChapterTreeAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var allChapters = query.Where(c => c.CourseId == courseId).OrderBy(c => c.SortOrder).ToList();

        if (allChapters.Count == 0)
        {
            return new List<ChapterDto>();
        }

        // 加载课程下所有知识资源，按 ChapterId 分组
        var resourceQuery = await _knowledgeResourceRepository.GetQueryableAsync();
        var allResources = resourceQuery
            .Where(r => r.CourseId == courseId)
            .ToList();

        // 加载关联的 Resource 文件信息（禁用多租户过滤器）
        Dictionary<Guid, (string? fileName, string? extension, long? size)> fileInfoMap;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var resourceIds = allResources
                .Where(r => r.ResourceId.HasValue)
                .Select(r => r.ResourceId!.Value)
                .Distinct()
                .ToList();
            var fileRepo = await _resourceRepository.GetQueryableAsync();
            var files = fileRepo
                .Where(f => resourceIds.Contains(f.Id))
                .Select(f => new { f.Id, f.OriginalFileName, f.FileExtension, f.FileSize })
                .ToList();
            fileInfoMap = files.ToDictionary(
                f => f.Id,
                f => (f.OriginalFileName, f.FileExtension, f.FileSize as long?));

            // 资源级文件元数据为空（旧数据 / 版本管理场景）时，从当前版本的 FilePath 回填
            // FileExtension / OriginalFileName / FileSize。
            // 与 ResourceAppService.EnsureFileMetadataFromCurrentVersionAsync 保持一致，
            // 否则课程学习页拿不到扩展名/大小，前端预览回退的 HEAD 探测又因
            // /api/resource-file/{id}/preview 不支持 HEAD 而直接失败（405）。
            var missingMetadata = files
                .Where(f => string.IsNullOrEmpty(f.FileExtension))
                .Select(f => f.Id)
                .ToList();
            if (missingMetadata.Count > 0)
            {
                var versions = (await _resourceVersionRepository.GetQueryableAsync())
                    .Where(v => missingMetadata.Contains(v.ResourceId) && v.IsCurrentVersion)
                    .ToList();
                foreach (var version in versions)
                {
                    if (string.IsNullOrEmpty(version.FilePath)) continue;
                    var info = fileInfoMap.TryGetValue(version.ResourceId, out var existing)
                        ? existing : (null, null, null);
                    fileInfoMap[version.ResourceId] = (
                        info.fileName ?? Path.GetFileName(version.FilePath),
                        info.extension ?? Path.GetExtension(version.FilePath)?.TrimStart('.'),
                        info.size ?? version.FileSize);
                }
            }
        }

        var resourcesByChapter = allResources
            .Where(r => r.ChapterId.HasValue)
            .GroupBy(r => r.ChapterId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chapterDtos = allChapters.Select(c => new ChapterDto
        {
            Id = c.Id,
            CourseId = c.CourseId,
            ParentId = c.ParentId,
            Title = c.Title,
            Description = c.Description,
            SortOrder = c.SortOrder,
            Children = new List<ChapterDto>(),
            KnowledgeResources = (resourcesByChapter.TryGetValue(c.Id, out var list)
                ? list.Select(r =>
                {
                    var fi = r.ResourceId.HasValue && fileInfoMap.TryGetValue(r.ResourceId.Value, out var info)
                        ? info : (null, null, null);
                    return new KnowledgeResourceDto
                    {
                        Id = r.Id,
                        CourseId = r.CourseId,
                        ChapterId = r.ChapterId,
                        Name = r.Name,
                        Description = r.Description,
                        Content = r.Content,
                        Difficulty = r.Difficulty,
                        ImportanceLevel = r.ImportanceLevel,
                        SortOrder = r.SortOrder,
                        Tags = r.Tags,
                        ParentId = r.ParentId,
                        ResourceId = r.ResourceId,
                        OriginalFileName = fi.fileName,
                        FileExtension = fi.extension,
                        FileSize = fi.size
                    };
                }).ToList()
                : new List<KnowledgeResourceDto>())
        }).ToList();

        return BuildTree(chapterDtos);
    }

    private List<ChapterDto> BuildTree(List<ChapterDto> chapters)
    {
        var lookup = chapters.ToDictionary(c => c.Id, c => c);
        var roots = new List<ChapterDto>();
        
        foreach (var chapter in chapters)
        {
            if (chapter.ParentId == null || !lookup.ContainsKey(chapter.ParentId.Value))
            {
                roots.Add(chapter);
            }
            else
            {
                var parent = lookup[chapter.ParentId.Value];
                parent.Children.Add(chapter);
            }
        }
        
        return roots;
    }

    public async Task DeleteByCourseAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Where(c => c.CourseId == courseId).ToList();

        // 同 DeleteAsync：先处理知识点资源，否则外键 NO ACTION 会把整批章节删除卡成 500。
        await DetachKnowledgeResourcesAsync(chapters.Select(c => c.Id).ToList());

        foreach (var chapter in chapters)
        {
            await _chapterRepository.DeleteAsync(chapter);
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file)
    {
        var result = new ChapterImportResultDto();

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // 获取有数据的行（跳过前两行：说明和表头）
            var rows = worksheet.RowsUsed().Skip(2).ToList();
            result.TotalRows = rows.Count;

            if (rows.Count == 0)
            {
                result.Errors.Add("Excel 文件中没有数据行");
                return result;
            }

            // 用于存储每一行对应的章节ID，按列索引（A=一级, B=二级, C=三级, D=四级, E=五级）
            var columnChapters = new Dictionary<int, Chapter>();

            // 当前 sortOrder
            var sortOrder = 0;

            foreach (var row in rows)
            {
                try
                {
                    // A-F 列分别对应 一级章节 到 五级章节 和 章节说明
                    var cellA = row.Cell(1).GetString(); // 一级章节
                    var cellB = row.Cell(2).GetString(); // 二级章节
                    var cellC = row.Cell(3).GetString(); // 三级章节
                    var cellD = row.Cell(4).GetString(); // 四级章节
                    var cellE = row.Cell(5).GetString(); // 五级章节
                    var cellF = row.Cell(6).GetString(); // 章节说明

                    // 按从右到左的顺序检查，找到最左边的非空列
                    string? title = null;
                    int level = 0;

                    if (!string.IsNullOrWhiteSpace(cellE)) { title = cellE.Trim(); level = 5; }
                    else if (!string.IsNullOrWhiteSpace(cellD)) { title = cellD.Trim(); level = 4; }
                    else if (!string.IsNullOrWhiteSpace(cellC)) { title = cellC.Trim(); level = 3; }
                    else if (!string.IsNullOrWhiteSpace(cellB)) { title = cellB.Trim(); level = 2; }
                    else if (!string.IsNullOrWhiteSpace(cellA)) { title = cellA.Trim(); level = 1; }

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue; // 跳过空行
                    }

                    // 确定父章节
                    Guid? parentId = null;
                    if (level > 1)
                    {
                        // 查找上一级（level-1）的章节作为父章节
                        if (columnChapters.TryGetValue(level - 1, out var parentChapter))
                        {
                            parentId = parentChapter.Id;
                        }
                    }

                    // 创建章节
                    var chapter = new Chapter(
                        GuidGenerator.Create(),
                        courseId,
                        title,
                        sortOrder++,
                        parentId
                    );
                    chapter.Description = string.IsNullOrWhiteSpace(cellF) ? null : cellF.Trim();

                    await _chapterRepository.InsertAsync(chapter);

                    // 更新当前级别的章节引用
                    columnChapters[level] = chapter;

                    // 清除比当前级别更深的章节引用（当出现更高级别的章节时）
                    for (int l = level + 1; l <= 5; l++)
                    {
                        columnChapters.Remove(l);
                    }

                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.Errors.Add($"行 {row.RowNumber()}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"解析 Excel 文件失败: {ex.Message}");
        }

        return result;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task ReorderChaptersAsync(List<ChapterOrderDto> orders)
    {
        foreach (var order in orders)
        {
            var chapter = await _chapterRepository.GetAsync(order.ChapterId);
            chapter.SortOrder = order.SortOrder;
            await _chapterRepository.UpdateAsync(chapter);
        }
    }
}
