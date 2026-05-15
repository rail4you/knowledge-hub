using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.TeachingAgents;

public class AgentRunMessage : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AgentRunId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ToolCallsJson { get; set; } = "[]";

    public AgentRunMessage()
    {
    }

    public AgentRunMessage(Guid id, Guid agentRunId, string role, string content) : base(id)
    {
        AgentRunId = agentRunId;
        Role = role;
        Content = content;
    }
}
