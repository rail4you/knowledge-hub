using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Majors.Dtos;

public class MajorDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? TrainingObjectives { get; set; }
}

public class MajorLookupDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class CreateUpdateMajorDto
{
    [Required]
    [StringLength(Major.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(Major.MaxCodeLength)]
    public string? Code { get; set; }

    [StringLength(Major.MaxDescriptionLength)]
    public string? Description { get; set; }

    [StringLength(Major.MaxTrainingObjectivesLength)]
    public string? TrainingObjectives { get; set; }
}

public class PagedMajorRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
