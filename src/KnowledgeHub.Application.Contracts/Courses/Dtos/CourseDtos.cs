using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

public class CourseDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Major { get; set; }
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public CourseStatus Status { get; set; }
    public int Difficulty { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? TeacherName { get; set; }
    public int ChapterCount { get; set; }
    public int StudentCount { get; set; }
}

public class CourseDetailDto : CourseDto
{
    public List<ChapterDto> Chapters { get; set; } = new();
}

public class CreateUpdateCourseDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Major { get; set; }
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public int Difficulty { get; set; } = 1;
    public Guid? CategoryId { get; set; }
}

public class PagedCourseRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Major { get; set; }
    public string? Semester { get; set; }
    public int? Difficulty { get; set; }
    public Guid? CategoryId { get; set; }
    public CourseStatus? Status { get; set; }
}

public class CourseFilterDto
{
    public string? Filter { get; set; }
    public string? Major { get; set; }
    public string? Semester { get; set; }
    public int? Difficulty { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TeacherId { get; set; }
    public CourseStatus? Status { get; set; }
}

public class AuditCourseDto
{
    public bool Approved { get; set; }
    public string? Comment { get; set; }
}

public class AuditResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
