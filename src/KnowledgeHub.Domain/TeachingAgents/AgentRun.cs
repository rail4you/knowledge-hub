using System;
using KnowledgeHub.TeachingAgents.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.TeachingAgents;

public class AgentRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ClassroomAgentAssignmentId { get; set; }
    public string ThreadId { get; set; } = string.Empty;
    public AgentRunStatus RuntimeStatus { get; set; } = AgentRunStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? LastError { get; set; }

    public AgentRun()
    {
    }

    public AgentRun(Guid id, Guid classroomAgentAssignmentId, string threadId) : base(id)
    {
        ClassroomAgentAssignmentId = classroomAgentAssignmentId;
        ThreadId = threadId;
    }
}
