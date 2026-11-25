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
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 教案的「思政融入点」：把已发布的思政案例（或自定义思政点）插到教案的某个环节。
///
/// <para>教案模块本身不知道本模块存在：这里只存 <c>LessonPlanId</c> 关联，
/// 教案删除后本表数据由应用层清理（见 <see cref="CleanupOrphansAsync"/> 可被定时任务调用，
/// 现阶段在读取时过滤掉已不存在的教案即可，不影响主流程）。</para>
/// </summary>
[Authorize(KnowledgeHubPermissions.Courses.Default)]
public class IdeologyInsetAppService : KnowledgeHubAppService, IIdeologyInsetAppService
{
    private const int MaxRecommendCount = 5;

    private readonly IRepository<IdeologyLessonPlanInset, Guid> _insetRepository;
    private readonly IRepository<IdeologyCase, Guid> _caseRepository;
    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IRepository<CourseLessonPlan, Guid> _lessonPlanRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public IdeologyInsetAppService(
        IRepository<IdeologyLessonPlanInset, Guid> insetRepository,
        IRepository<IdeologyCase, Guid> caseRepository,
        IRepository<IdeologyElement, Guid> elementRepository,
        IRepository<CourseLessonPlan, Guid> lessonPlanRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _insetRepository = insetRepository;
        _caseRepository = caseRepository;
        _elementRepository = elementRepository;
        _lessonPlanRepository = lessonPlanRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<List<IdeologyInsetDto>> GetByLessonPlanAsync(Guid lessonPlanId)
    {
        var query = await _insetRepository.GetQueryableAsync();
        var insets = await AsyncExecuter.ToListAsync(
            query.Where(x => x.LessonPlanId == lessonPlanId)
                .OrderBy(x => x.Position)
                .ThenBy(x => x.SortOrder));

        if (insets.Count == 0)
        {
            return new List<IdeologyInsetDto>();
        }

        var caseIds = insets.Where(x => x.CaseId.HasValue).Select(x => x.CaseId!.Value).Distinct().ToList();
        var caseQuery = await _caseRepository.GetQueryableAsync();
        var cases = await AsyncExecuter.ToListAsync(caseQuery.Where(x => caseIds.Contains(x.Id)));
        var sourceMap = cases.ToDictionary(x => x.Id, x => x.SourceText);

        return insets.Select(x => new IdeologyInsetDto
        {
            Id = x.Id,
            LessonPlanId = x.LessonPlanId,
            CourseId = x.CourseId,
            CaseId = x.CaseId,
            ElementId = x.ElementId,
            Position = x.Position,
            Title = x.Title,
            Content = x.Content,
            SortOrder = x.SortOrder,
            SourceText = x.CaseId.HasValue ? sourceMap.GetValueOrDefault(x.CaseId.Value) : null,
        }).ToList();
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<IdeologyInsetDto> CreateAsync(CreateIdeologyInsetDto input)
    {
        var lessonPlan = await _lessonPlanRepository.FindAsync(input.LessonPlanId)
                         ?? throw new UserFriendlyException("教案不存在或已删除。");

        IdeologyCase? ideologyCase = null;
        if (input.CaseId.HasValue)
        {
            ideologyCase = await _caseRepository.FindAsync(input.CaseId.Value)
                           ?? throw new UserFriendlyException("思政案例不存在或已删除。");
            if (ideologyCase.CourseId != lessonPlan.CourseId)
            {
                throw new UserFriendlyException("只能引用本课程的思政案例。");
            }
        }

        var hasContent = !string.IsNullOrWhiteSpace(input.Content) || !string.IsNullOrWhiteSpace(input.Title);
        if (ideologyCase == null && !hasContent)
        {
            throw new UserFriendlyException("请选择思政案例，或填写自定义思政点内容。");
        }

        var inset = new IdeologyLessonPlanInset(GuidGenerator.Create(), lessonPlan.Id, lessonPlan.CourseId)
        {
            TenantId = CurrentTenant.Id,
            CaseId = ideologyCase?.Id,
            ElementId = input.ElementId,
            Position = input.Position,
            // 引用案例时把标题/说明快照下来：案例以后被改名/删除，教案里的思政点仍然完整。
            Title = string.IsNullOrWhiteSpace(input.Title) ? ideologyCase?.Title : input.Title.Trim(),
            Content = string.IsNullOrWhiteSpace(input.Content) ? ideologyCase?.Summary : input.Content!.Trim(),
            SortOrder = input.SortOrder,
        };

        await _insetRepository.InsertAsync(inset, autoSave: true);

        return new IdeologyInsetDto
        {
            Id = inset.Id,
            LessonPlanId = inset.LessonPlanId,
            CourseId = inset.CourseId,
            CaseId = inset.CaseId,
            ElementId = inset.ElementId,
            Position = inset.Position,
            Title = inset.Title,
            Content = inset.Content,
            SortOrder = inset.SortOrder,
            SourceText = ideologyCase?.SourceText,
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _insetRepository.DeleteAsync(id);
    }

    public async Task<List<IdeologyRecommendItemDto>> RecommendAsync(Guid lessonPlanId)
    {
        var lessonPlan = await _lessonPlanRepository.FindAsync(lessonPlanId)
                         ?? throw new UserFriendlyException("教案不存在或已删除。");

        var caseQuery = await _caseRepository.WithDetailsAsync(x => x.Elements);
        var query = caseQuery.Where(x =>
            x.CourseId == lessonPlan.CourseId && x.Status == IdeologyCaseStatus.Published);

        // 章节优先级：同章节 → 课程通用（未绑章节）；其余章节不推荐，避免"跨章节硬塞"。
        var chapterId = lessonPlan.ChapterId;
        var candidates = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime));

        var ranked = candidates
            .Where(x => x.ChapterId == chapterId || !x.ChapterId.HasValue)
            .OrderByDescending(x => chapterId.HasValue && x.ChapterId == chapterId)
            .ThenByDescending(x => x.PublishedAt ?? x.CreationTime)
            .Take(MaxRecommendCount)
            .ToList();

        if (ranked.Count == 0)
        {
            return new List<IdeologyRecommendItemDto>();
        }

        var elementIds = ranked.SelectMany(x => x.Elements).Select(x => x.ElementId).Distinct().ToList();
        var elementQuery = await _elementRepository.GetQueryableAsync();
        var elementNames = (await AsyncExecuter.ToListAsync(elementQuery.Where(x => elementIds.Contains(x.Id))))
            .ToDictionary(x => x.Id, x => x.Name);

        // 章节标题用于生成"推荐理由"。
        string? chapterTitle = lessonPlan.ChapterTitle;
        if (lessonPlan.ChapterId.HasValue && string.IsNullOrWhiteSpace(chapterTitle))
        {
            var chapter = await _chapterRepository.FindAsync(lessonPlan.ChapterId.Value);
            chapterTitle = chapter?.Title;
        }

        return ranked.Select(x => new IdeologyRecommendItemDto
        {
            CaseId = x.Id,
            Title = x.Title,
            Summary = x.Summary,
            UsageMethod = x.UsageMethod,
            SourceText = x.SourceText,
            ElementNames = x.Elements
                .Select(e => elementNames.GetValueOrDefault(e.ElementId))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList(),
            Reason = x.ChapterId.HasValue && x.ChapterId == chapterId
                ? $"同章节「{chapterTitle ?? x.ChapterTitle}」的已发布案例"
                : "课程通用案例（未绑章节）",
        }).ToList();
    }
}
