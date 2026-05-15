using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.TeachingAgents;

public class TeachingAgentVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid TeachingAgentId { get; set; }
    public int VersionNumber { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string? WelcomeMessage { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string SkillsJson { get; set; } = "[]";
    public string? VersionNote { get; set; }
    public bool IsPublished { get; set; }

    public TeachingAgentVersion()
    {
    }

    public TeachingAgentVersion(Guid id, Guid teachingAgentId, int versionNumber) : base(id)
    {
        TeachingAgentId = teachingAgentId;
        VersionNumber = versionNumber;
    }
}
