using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.CourseIdeology.Dtos;
using KnowledgeHub.CourseIdeology.Enums;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 思政案例库（**强绑课程**、不做审核）。
///
/// <para>老师建完就是自己的素材，可保存为草稿也可以直接发布；发布后可被图谱 / 覆盖分析 / 教案推荐使用。</para>
/// </summary>
[Authorize(KnowledgeHubPermissions.CourseIdeology.Default)]
public class IdeologyCaseAppService : KnowledgeHubAppService, IIdeologyCaseAppService
{
    private readonly IRepository<IdeologyCase, Guid> _caseRepository;
    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public IdeologyCaseAppService(
        IRepository<IdeologyCase, Guid> caseRepository,
        IRepository<IdeologyElement, Guid> elementRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _caseRepository = caseRepository;
        _elementRepository = elementRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<PagedResultDto<IdeologyCaseDto>> GetListAsync(IdeologyCaseQueryDto input)
    {
        if (input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程。");
        }

        var query = await _caseRepository.WithDetailsAsync(x => x.Elements);
        query = query.Where(x => x.CourseId == input.CourseId);

        if (input.ChapterId.HasValue)
        {
            query = query.Where(x => x.ChapterId == input.ChapterId.Value);
        }

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var keyword = input.Filter.Trim();
            query = query.Where(x =>
                x.Title.Contains(keyword) ||
                x.SourceText.Contains(keyword) ||
                (x.Summary != null && x.Summary.Contains(keyword)));
        }

        if (input.ElementId.HasValue)
        {
            var elementId = input.ElementId.Value;
            query = query.Where(x => x.Elements.Any(e => e.ElementId == elementId));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var cases = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));

        var elementNames = await GetElementNameMapAsync();
        return new PagedResultDto<IdeologyCaseDto>(
            totalCount,
            cases.Select(x => MapToDto(x, elementNames)).ToList());
    }

    public async Task<IdeologyCaseDto> GetAsync(Guid id)
    {
        var query = await _caseRepository.WithDetailsAsync(x => x.Elements);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
                     ?? throw new UserFriendlyException("思政案例不存在或已删除。");

        return MapToDto(entity, await GetElementNameMapAsync());
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.Edit)]
    public async Task<IdeologyCaseDto> CreateAsync(CreateUpdateIdeologyCaseDto input)
    {
        var course = await EnsureCourseAsync(input.CourseId);
        var chapterTitle = await ResolveChapterTitleAsync(input.CourseId, input.ChapterId);

        var entity = new IdeologyCase(GuidGenerator.Create(), input.CourseId, NormalizeTitle(input.Title))
        {
            TenantId = CurrentTenant.Id,
            ChapterId = input.ChapterId,
            ChapterTitle = chapterTitle,
            CreatorName = CurrentUser.Name ?? CurrentUser.UserName,
        };

        ApplyInput(entity, input);
        entity.SetElements(input.ElementIds);

        if (input.Publish)
        {
            entity.Publish(Clock.Now);
        }

        await _caseRepository.InsertAsync(entity, autoSave: true);

        // 章节快照兜底：章节被删/改名时用快照展示（这里只是为了返回 DTO 时不为空）。
        _ = course;
        return await GetAsync(entity.Id);
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.Edit)]
    public async Task<IdeologyCaseDto> UpdateAsync(Guid id, CreateUpdateIdeologyCaseDto input)
    {
        var query = await _caseRepository.WithDetailsAsync(x => x.Elements);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
                     ?? throw new UserFriendlyException("思政案例不存在或已删除。");

        await EnsureCourseAsync(input.CourseId);

        entity.CourseId = input.CourseId;
        entity.ChapterId = input.ChapterId;
        entity.ChapterTitle = await ResolveChapterTitleAsync(input.CourseId, input.ChapterId);
        entity.Title = NormalizeTitle(input.Title);

        ApplyInput(entity, input);
        entity.SetElements(input.ElementIds);

        if (input.Publish)
        {
            entity.Publish(Clock.Now);
        }

        await _caseRepository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _caseRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.Edit)]
    public async Task<IdeologyCaseDto> PublishAsync(Guid id)
    {
        var entity = await _caseRepository.GetAsync(id);
        entity.Publish(Clock.Now);
        await _caseRepository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.Edit)]
    public async Task<IdeologyCaseDto> ArchiveAsync(Guid id)
    {
        var entity = await _caseRepository.GetAsync(id);
        entity.Archive();
        await _caseRepository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    // ── 内部辅助 ────────────────────────────────────────────

    private static void ApplyInput(IdeologyCase entity, CreateUpdateIdeologyCaseDto input)
    {
        entity.Summary = TrimToNull(input.Summary);
        entity.ContentJson = TrimToNull(input.ContentJson);
        entity.SourceType = input.SourceType;

        // 出处选填：留空时默认用所属章节名（老师最常给的"出处"就是教材章节）。
        var sourceText = TrimToNull(input.SourceText)
                         ?? (string.IsNullOrWhiteSpace(entity.ChapterTitle) ? null : entity.ChapterTitle);
        entity.SourceText = TrimToLimit(sourceText, IdeologyCase.MaxSourceTextLength) ?? string.Empty;
        entity.SourceUrl = string.IsNullOrWhiteSpace(input.SourceUrl)
            ? null
            : TrimToLimit(input.SourceUrl, IdeologyCase.MaxSourceUrlLength);
        entity.SourceDate = input.SourceDate;
        entity.UsageMethod = input.UsageMethod;
        entity.AiGenerated = input.AiGenerated;
        entity.AiMaterial = TrimToLimit(input.AiMaterial, 2000);
    }

    private async Task<Course> EnsureCourseAsync(Guid courseId)
    {
        if (courseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程。");
        }

        var course = await _courseRepository.FindAsync(courseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在或无权访问。");
        }

        return course;
    }

    private async Task<string?> ResolveChapterTitleAsync(Guid courseId, Guid? chapterId)
    {
        if (!chapterId.HasValue)
        {
            return null;
        }

        var chapter = await _chapterRepository.FindAsync(chapterId.Value);
        if (chapter == null || chapter.CourseId != courseId)
        {
            throw new UserFriendlyException("章节不存在或不属于该课程。");
        }

        return chapter.Title;
    }

    private async Task<Dictionary<Guid, string>> GetElementNameMapAsync()
    {
        var elements = await _elementRepository.GetListAsync();
        return elements.ToDictionary(x => x.Id, x => x.Name);
    }

    private IdeologyCaseDto MapToDto(IdeologyCase entity, Dictionary<Guid, string> elementNames)
    {
        var elementIds = entity.Elements.Select(x => x.ElementId).ToList();

        return new IdeologyCaseDto
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            ChapterId = entity.ChapterId,
            ChapterTitle = entity.ChapterTitle,
            Title = entity.Title,
            Summary = entity.Summary,
            ContentJson = entity.ContentJson,
            SourceType = entity.SourceType,
            SourceText = entity.SourceText,
            SourceUrl = entity.SourceUrl,
            SourceDate = entity.SourceDate,
            UsageMethod = entity.UsageMethod,
            Status = entity.Status,
            AiGenerated = entity.AiGenerated,
            AiMaterial = entity.AiMaterial,
            CreatorName = entity.CreatorName,
            PublishedAt = entity.PublishedAt,
            CreationTime = entity.CreationTime,
            ElementIds = elementIds,
            ElementNames = elementIds
                .Select(id => elementNames.GetValueOrDefault(id))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList(),
        };
    }

    private static string NormalizeTitle(string? title)
    {
        var value = (title ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new UserFriendlyException("案例标题不能为空。");
        }

        return value.Length > IdeologyCase.MaxTitleLength ? value[..IdeologyCase.MaxTitleLength] : value;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TrimToLimit(string? value, int max)
    {
        var trimmed = TrimToNull(value);
        if (trimmed == null)
        {
            return null;
        }

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
