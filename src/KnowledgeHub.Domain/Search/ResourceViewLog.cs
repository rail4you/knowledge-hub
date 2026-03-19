using System;
using KnowledgeHub.Domain.Search.Enums;
using KnowledgeHub.Resources;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Domain.Search;

public class ResourceViewLog : FullAuditedAggregateRoot<Guid>
{
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public int? PageNumber { get; set; }
    public int ViewDurationSeconds { get; set; }
    public ViewSource ViewSource { get; set; }
    public Guid? TenantId { get; set; }

    public virtual Resource? Resource { get; set; }
}
