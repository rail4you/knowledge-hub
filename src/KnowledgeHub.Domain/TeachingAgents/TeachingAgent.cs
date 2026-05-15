using System;
using KnowledgeHub.TeachingAgents.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.TeachingAgents;

public class TeachingAgent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public TeachingAgentVisibility Visibility { get; set; } = TeachingAgentVisibility.Private;
    public TeachingAgentStatus Status { get; set; } = TeachingAgentStatus.Draft;
    public Guid? PublishedVersionId { get; set; }

    public TeachingAgent()
    {
    }

    public TeachingAgent(Guid id, Guid ownerUserId, string name) : base(id)
    {
        OwnerUserId = ownerUserId;
        Name = name;
    }
}
