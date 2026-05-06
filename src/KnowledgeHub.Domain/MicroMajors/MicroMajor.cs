using System;
using KnowledgeHub.MicroMajors.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MicroMajors;

public class MicroMajor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? IndustryField { get; set; }
    public string? CollaborationUnit { get; set; }
    public MicroMajorStatus Status { get; set; } = MicroMajorStatus.Draft;
    public decimal RequiredCompletionRate { get; set; } = 100;
    public bool IsCertificateEnabled { get; set; } = true;

    public MicroMajor()
    {
    }

    public MicroMajor(Guid id, string title)
        : base(id)
    {
        Title = title;
    }
}
