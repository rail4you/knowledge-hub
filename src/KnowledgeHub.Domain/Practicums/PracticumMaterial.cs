using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumMaterial : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PracticumMaterialType MaterialType { get; set; } = PracticumMaterialType.Guide;
    public string ResourceUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public PracticumMaterial()
    {
    }

    public PracticumMaterial(Guid id, Guid projectId, string title, string resourceUrl)
        : base(id)
    {
        ProjectId = projectId;
        Title = title;
        ResourceUrl = resourceUrl;
    }
}
