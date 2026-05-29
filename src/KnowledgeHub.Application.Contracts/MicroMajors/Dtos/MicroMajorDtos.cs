using System;
using System.Collections.Generic;
using KnowledgeHub.MicroMajors.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.MicroMajors.Dtos;

public class MicroMajorDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? IndustryField { get; set; }
    public string? CollaborationUnit { get; set; }
    public MicroMajorStatus Status { get; set; }
    public decimal RequiredCompletionRate { get; set; }
    public bool IsCertificateEnabled { get; set; }
    public int CourseCount { get; set; }
    public int EnrollmentCount { get; set; }
    public decimal? CurrentUserProgress { get; set; }
    public bool IsCurrentUserEnrolled { get; set; }
}

public class MicroMajorDetailDto : MicroMajorDto
{
    public List<MicroMajorCourseDto> Courses { get; set; } = new();
}

public class MicroMajorCourseDto : EntityDto<Guid>
{
    public Guid MicroMajorId { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string? CourseCoverImageUrl { get; set; }
    public string? Major { get; set; }
    public string? Semester { get; set; }
    public int SortOrder { get; set; }
    public bool IsCore { get; set; }
}

public class CreateUpdateMicroMajorCourseDto
{
    public Guid CourseId { get; set; }
    public int SortOrder { get; set; }
    public bool IsCore { get; set; } = true;
}

public class CreateUpdateMicroMajorDto
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? IndustryField { get; set; }
    public string? CollaborationUnit { get; set; }
    public MicroMajorStatus Status { get; set; } = MicroMajorStatus.Draft;
    public decimal RequiredCompletionRate { get; set; } = 100;
    public bool IsCertificateEnabled { get; set; } = true;
    public List<CreateUpdateMicroMajorCourseDto> Courses { get; set; } = new();
}

public class PagedMicroMajorRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public MicroMajorStatus? Status { get; set; }
}

public class MicroMajorResourceDto
{
    public Guid Id { get; set; }
    public Guid MicroMajorId { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public int DownloadCount { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}
