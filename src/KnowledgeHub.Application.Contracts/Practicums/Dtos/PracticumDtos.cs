using System;
using System.Collections.Generic;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Practicums.Dtos;

public class PracticumTaskDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Requirement { get; set; }
    public DateTime? DueTime { get; set; }
    public decimal ScoreWeight { get; set; }
    public int SortOrder { get; set; }
}

public class PracticumMaterialDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PracticumMaterialType MaterialType { get; set; }
    public string ResourceUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class PracticumProjectDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string? Major { get; set; }
    public string? ClassName { get; set; }
    public PracticumProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal MaxScore { get; set; }
    public bool AllowResubmission { get; set; }
    public int TaskCount { get; set; }
    public int MaterialCount { get; set; }
    public int EnrollmentCount { get; set; }
    public bool IsCurrentUserEnrolled { get; set; }
    public decimal? CurrentUserProgress { get; set; }
}

public class PracticumProjectDetailDto : PracticumProjectDto
{
    public List<PracticumTaskDto> Tasks { get; set; } = new();
    public List<PracticumMaterialDto> Materials { get; set; } = new();
}

public class CreateUpdatePracticumTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Requirement { get; set; }
    public DateTime? DueTime { get; set; }
    public decimal ScoreWeight { get; set; }
    public int SortOrder { get; set; }
}

public class CreateUpdatePracticumMaterialDto
{
    public Guid? TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PracticumMaterialType MaterialType { get; set; }
    public string ResourceUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateUpdatePracticumProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? CourseId { get; set; }
    public string? Major { get; set; }
    public string? ClassName { get; set; }
    public PracticumProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal MaxScore { get; set; } = 100;
    public bool AllowResubmission { get; set; } = true;
    public List<CreateUpdatePracticumTaskDto> Tasks { get; set; } = new();
    public List<CreateUpdatePracticumMaterialDto> Materials { get; set; } = new();
}

public class PagedPracticumProjectRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CourseId { get; set; }
    public PracticumProjectStatus? Status { get; set; }
}
