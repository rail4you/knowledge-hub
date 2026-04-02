using System;
using KnowledgeHub.Resources;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Domain.Search;

public class ResourceReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string? Content { get; set; }
    public Guid? TenantId { get; set; }

    public virtual Resource? Resource { get; set; }
}
