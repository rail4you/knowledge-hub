using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses.Dtos;

public interface IKnowledgeResourceAppService : IApplicationService
{
    Task<List<KnowledgeResourceDto>> GetByCourseAsync(Guid courseId);
    Task<List<KnowledgeResourceDto>> GetByChapterAsync(Guid chapterId);

    /// <summary>课程知识点树（一次查询建树，按 SortPath / SortOrder 排序）。</summary>
    Task<List<KnowledgeResourceDto>> GetTreeAsync(Guid courseId);

    Task<RelatedCoursesResultDto> GetRelatedCoursesAsync(Guid knowledgeResourceId);
    Task<KnowledgeResourceDto> CreateAsync(CreateUpdateKnowledgeResourceDto input);
    Task<KnowledgeResourceDto> UpdateAsync(Guid id, CreateUpdateKnowledgeResourceDto input);
    Task DeleteAsync(Guid id);

    /// <summary>移动节点（改父级 + 目标位置），自动维护物化路径并校验环路 / 跨课程。</summary>
    Task<KnowledgeResourceDto> MoveAsync(Guid id, MoveKnowledgeResourceInput input);

    /// <summary>同级批量重排（拖拽后一次性提交，减少往返）。</summary>
    Task<List<KnowledgeResourceDto>> BatchReorderAsync(BatchReorderKnowledgeResourcesInput input);

    /// <summary>重算整门课程的物化路径（历史数据 / 路径异常时的修复入口）。</summary>
    Task<int> RegenerateSortPathsAsync(Guid courseId);

    /// <summary>
    /// 从 xlsx 导入知识点树（A/B/C 列=单元，D 列=知识项；同父级同名复用，可重复导入）。
    /// 只支持 xlsx，不支持 XMind / OPML。
    /// </summary>
    Task<KnowledgeResourceImportResultDto> ImportFromExcelAsync(Guid courseId, Microsoft.AspNetCore.Http.IFormFile file);

    /// <summary>下载导入模板（表头 + 2 行示例 + 填写说明）。</summary>
    Task<Volo.Abp.Content.IRemoteStreamContent> GetImportTemplateAsync();
}

public class RelatedCoursesResultDto
{
    public Guid KnowledgeResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<RelatedCourseInfoDto> Courses { get; set; } = new();
}

public class RelatedCourseInfoDto
{
    public Guid CourseId { get; set; }
    public List<RelatedChapterInfoDto> Chapters { get; set; } = new();
}

public class RelatedChapterInfoDto
{
    public Guid ChapterId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
}
