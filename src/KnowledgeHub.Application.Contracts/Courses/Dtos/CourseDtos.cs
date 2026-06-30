using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

public class CourseDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
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
    public bool IsEnrolled { get; set; }
    public decimal Progress { get; set; }
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
    public Guid? MajorId { get; set; }
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public int Difficulty { get; set; } = 1;
    public Guid? CategoryId { get; set; }
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
}

public class PagedCourseRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? MajorId { get; set; }
    public string? Semester { get; set; }
    public int? Difficulty { get; set; }
    public Guid? CategoryId { get; set; }
    public CourseStatus? Status { get; set; }
}

public class CourseFilterDto
{
    public string? Filter { get; set; }
    public Guid? MajorId { get; set; }
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

public class StudentCourseDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }
    public StudentCourseStatus Status { get; set; }
    public DateTime EnrolledAt { get; set; }
    public decimal Progress { get; set; }
}

public class GetStudentCoursesInput : PagedAndSortedResultRequestDto
{
    public Guid? CourseId { get; set; }
    public Guid? StudentId { get; set; }
    public StudentCourseStatus? Status { get; set; }
    public Guid? TenantId { get; set; }
    public string? Filter { get; set; }
}

public class CreateStudentCourseDto
{
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
}

public class BatchEnrollDto
{
    public List<Guid> StudentIds { get; set; } = new();
    public Guid CourseId { get; set; }
}

public class GetAvailableStudentsInput : PagedAndSortedResultRequestDto
{
    public Guid CourseId { get; set; }
    public Guid? TenantId { get; set; }
    public string? Filter { get; set; }
    public Guid? MajorId { get; set; }
}
