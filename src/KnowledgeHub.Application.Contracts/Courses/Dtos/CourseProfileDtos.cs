using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses.Dtos;

// ══════════════════════════════════════════════════════════════════
//  课程教学信息（CourseInfo）
// ══════════════════════════════════════════════════════════════════

public interface ICourseInfoAppService : IApplicationService
{
    /// <summary>取课程教学信息；未维护时返回空对象（CourseId 已填），前端按空态处理。</summary>
    Task<CourseInfoDto> GetByCourseAsync(Guid courseId);

    /// <summary>保存（不存在则新建）课程教学信息。</summary>
    Task<CourseInfoDto> SaveAsync(SaveCourseInfoDto input);
}

public class CourseInfoDto
{
    public Guid CourseId { get; set; }
    public string? Background { get; set; }
    public string? Objectives { get; set; }
    public string? Target { get; set; }
    public string? Highlights { get; set; }
    public string? Introduction { get; set; }
    public string? Structure { get; set; }
    public string? GraduationRequirements { get; set; }

    /// <summary>是否已有内容（前端据此决定是否显示对应 Tab）。</summary>
    public bool HasContent { get; set; }
}

public class SaveCourseInfoDto
{
    public Guid CourseId { get; set; }
    public string? Background { get; set; }
    public string? Objectives { get; set; }
    public string? Target { get; set; }
    public string? Highlights { get; set; }
    public string? Introduction { get; set; }
    public string? Structure { get; set; }
    public string? GraduationRequirements { get; set; }
}

// ══════════════════════════════════════════════════════════════════
//  课程教材（CourseTextbook）
// ══════════════════════════════════════════════════════════════════

public interface ICourseTextbookAppService : IApplicationService
{
    Task<List<CourseTextbookDto>> GetByCourseAsync(Guid courseId);
    Task<CourseTextbookDto> CreateAsync(SaveCourseTextbookDto input);
    Task<CourseTextbookDto> UpdateAsync(Guid id, SaveCourseTextbookDto input);
    Task DeleteAsync(Guid id);

    /// <summary>把某本设为课程主教材（其它教材自动取消主教材标记）。</summary>
    Task<CourseTextbookDto> SetPrimaryAsync(Guid id);
}

public class CourseTextbookDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? Edition { get; set; }
    public string? Isbn { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? AttachmentResourceId { get; set; }

    /// <summary>附件原始文件名（便于学生端直接显示可下载的名称）。</summary>
    public string? AttachmentFileName { get; set; }

    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class SaveCourseTextbookDto
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? Edition { get; set; }
    public string? Isbn { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? AttachmentResourceId { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}
