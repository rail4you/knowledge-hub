using System;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.MicroMajors.Dtos;

public class MicroMajorCertificateTemplateDto : FullAuditedEntityDto<Guid>
{
    public Guid MicroMajorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateUpdateMicroMajorCertificateTemplateDto
{
    public Guid MicroMajorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}