using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.CourseIdeology.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 课程思政 · 思政元素体系。
///
/// <para>元素体系按租户可配置（默认种子 6 个通用元素），案例通过多对多关联到元素，
/// 图谱与覆盖分析都基于这套元素统计。</para>
/// </summary>
public interface IIdeologyElementAppService : IApplicationService
{
    /// <summary>元素列表（含关联案例数；<paramref name="onlyEnabled"/> = true 时只返回启用的）。</summary>
    Task<List<IdeologyElementDto>> GetListAsync(bool onlyEnabled = false);

    Task<IdeologyElementDto> CreateAsync(CreateUpdateIdeologyElementDto input);

    Task<IdeologyElementDto> UpdateAsync(Guid id, CreateUpdateIdeologyElementDto input);

    /// <summary>删除元素（同时清掉与案例的关联）。</summary>
    Task DeleteAsync(Guid id);
}

/// <summary>
/// 课程思政 · 思政案例库（**强绑课程**、**不做审核**：老师自建自用，草稿/发布两态）。
/// </summary>
public interface IIdeologyCaseAppService : IApplicationService
{
    Task<PagedResultDto<IdeologyCaseDto>> GetListAsync(IdeologyCaseQueryDto input);

    Task<IdeologyCaseDto> GetAsync(Guid id);

    Task<IdeologyCaseDto> CreateAsync(CreateUpdateIdeologyCaseDto input);

    Task<IdeologyCaseDto> UpdateAsync(Guid id, CreateUpdateIdeologyCaseDto input);

    Task DeleteAsync(Guid id);

    /// <summary>发布（草稿 → 已发布）。</summary>
    Task<IdeologyCaseDto> PublishAsync(Guid id);

    /// <summary>下架（已发布 → 已下架，不再参与统计与推荐）。</summary>
    Task<IdeologyCaseDto> ArchiveAsync(Guid id);
}

/// <summary>课程思政 · 思政图谱（章节 ⇄ 案例 ⇄ 元素，只读；关联的增删走案例的编辑接口）。</summary>
public interface IIdeologyGraphAppService : IApplicationService
{
    Task<IdeologyGraphDto> GetAsync(Guid courseId);
}

/// <summary>课程思政 · 覆盖分析（章节覆盖 / 元素分布 / 盲区 + xlsx 导出）。</summary>
public interface IIdeologyCoverageAppService : IApplicationService
{
    Task<IdeologyCoverageDto> GetAsync(Guid courseId);

    /// <summary>导出覆盖分析报告（xlsx，含「总览 / 章节覆盖 / 元素分布 / 案例清单」4 个 Sheet）。</summary>
    Task<IRemoteStreamContent> ExportAsync(Guid courseId);
}

/// <summary>课程思政 · 课程级绑定设置（是否启用 + 课程思政总目标）。</summary>
public interface ICourseIdeologySettingAppService : IApplicationService
{
    Task<CourseIdeologySettingDto> GetAsync(Guid courseId);

    Task<CourseIdeologySettingDto> SaveAsync(SaveCourseIdeologySettingDto input);
}

/// <summary>课程思政 · 教案融入点（教案页右侧面板用）。</summary>
public interface IIdeologyInsetAppService : IApplicationService
{
    Task<List<IdeologyInsetDto>> GetByLessonPlanAsync(Guid lessonPlanId);

    Task<IdeologyInsetDto> CreateAsync(CreateIdeologyInsetDto input);

    Task DeleteAsync(Guid id);

    /// <summary>按教案所属章节推荐已发布案例（最多 5 条）。</summary>
    Task<List<IdeologyRecommendItemDto>> RecommendAsync(Guid lessonPlanId);
}

/// <summary>课程思政 · AI 生成案例草稿（同步返回，教师确认后才落库）。</summary>
public interface IIdeologyAiAppService : IApplicationService
{
    Task<IdeologyCaseDraftDto> GenerateDraftAsync(GenerateIdeologyCaseInputDto input);
}
