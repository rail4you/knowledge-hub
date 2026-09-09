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
    /// <summary>主专业（兼容老字段；为空表示公共课）</summary>
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    /// <summary>归属的全部专业（含主专业），为空表示公共课</summary>
    public List<Guid> MajorIds { get; set; } = new();
    /// <summary>与 MajorIds 一一对应的专业名（主专业排第一）</summary>
    public List<string> MajorNames { get; set; } = new();
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public CourseStatus Status { get; set; }
    public int Difficulty { get; set; }
    /// <summary>是否推荐课程</summary>
    public bool IsRecommended { get; set; }
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
    /// <summary>主专业（兼容老字段；传 MajorIds 时以后端归一化结果为准）</summary>
    public Guid? MajorId { get; set; }
    /// <summary>归属的全部专业，主专业必须包含在内；空列表表示公共课</summary>
    public List<Guid> MajorIds { get; set; } = new();
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
    public int Difficulty { get; set; } = 1;
    public Guid? CategoryId { get; set; }
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    /// <summary>是否推荐课程</summary>
    public bool IsRecommended { get; set; }
}

public class PagedCourseRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    /// <summary>按专业筛选：命中该专业（含兼属）或公共课（无归属专业）</summary>
    public Guid? MajorId { get; set; }
    /// <summary>按多个专业筛选：命中任一专业（含兼属）或公共课</summary>
    public List<Guid>? MajorIds { get; set; }
    /// <summary>只返回公共课（无任何专业归属）时传 true；与 MajorId/MajorIds 互斥且优先</summary>
    public bool? OnlyPublicCourses { get; set; }
    public string? Semester { get; set; }
    public int? Difficulty { get; set; }
    public Guid? CategoryId { get; set; }
    public CourseStatus? Status { get; set; }
    /// <summary>只返回推荐课程时传 true；null 表示不过滤</summary>
    public bool? IsRecommended { get; set; }
    public Guid? TenantId { get; set; }
}

public class CourseFilterDto
{
    public string? Filter { get; set; }
    /// <summary>按专业筛选：命中该专业（含兼属）或公共课（无归属专业）</summary>
    public Guid? MajorId { get; set; }
    /// <summary>按多个专业筛选：命中任一专业（含兼属）或公共课</summary>
    public List<Guid>? MajorIds { get; set; }
    public string? Semester { get; set; }
    public int? Difficulty { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TeacherId { get; set; }
    public CourseStatus? Status { get; set; }
    /// <summary>只返回推荐课程时传 true；null 表示不过滤</summary>
    public bool? IsRecommended { get; set; }
    public Guid? TenantId { get; set; }
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
