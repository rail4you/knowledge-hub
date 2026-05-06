using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighEvidence : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid IndicatorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighEvidenceType EvidenceType { get; set; } = DoubleHighEvidenceType.ResourceLink;
    public Guid? ResourceId { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? ExternalLink { get; set; }
    public int SortOrder { get; set; }

    public DoubleHighEvidence()
    {
    }

    public DoubleHighEvidence(Guid id, Guid projectId, Guid indicatorId, string title)
        : base(id)
    {
        ProjectId = projectId;
        IndicatorId = indicatorId;
        Title = title;
    }
}
