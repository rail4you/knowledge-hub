using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

/// <summary>课程共享记录。</summary>
public class CourseShareDto : EntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public Guid SourceTenantId { get; set; }
    public string? SourceTenantName { get; set; }
    public Guid TargetTenantId { get; set; }
    public string? TargetTenantName { get; set; }
    public Guid SharedByUserId { get; set; }
    public string? SharedByUserName { get; set; }
    public DateTime SharedAt { get; set; }
    public string? Note { get; set; }
}

/// <summary>创建课程共享。</summary>
public class CreateCourseShareDto
{
    public Guid CourseId { get; set; }
    public List<Guid> TargetTenantIds { get; set; } = new();
    public string? Note { get; set; }
}

/// <summary>共享课程（跨租户视图）。</summary>
public class SharedCourseDto : EntityDto<Guid>
{
    public Guid ShareId { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public Guid? LibraryId { get; set; }
    public Guid SourceTenantId { get; set; }
    public string? SourceTenantName { get; set; }
    public DateTime SharedAt { get; set; }
    public Guid SharedByUserId { get; set; }
    public string? SharedByUserName { get; set; }
}

/// <summary>共享课程列表查询条件。</summary>
public class SharedCourseListQueryDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? MajorId { get; set; }
}
