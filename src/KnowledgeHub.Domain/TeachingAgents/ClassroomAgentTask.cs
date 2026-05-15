using System;
using KnowledgeHub.TeachingAgents.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.TeachingAgents;

public class ClassroomAgentTask : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TeachingAgentId { get; set; }
    public Guid TeachingAgentVersionId { get; set; }
    public string TaskPrompt { get; set; } = string.Empty;
    public ClassroomAgentTaskTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string TargetSnapshotJson { get; set; } = "{}";
    public DateTime? DueTime { get; set; }
    public ClassroomAgentTaskPublishStatus PublishStatus { get; set; } = ClassroomAgentTaskPublishStatus.Draft;
    public Guid CreatorUserId { get; set; }

    public ClassroomAgentTask()
    {
    }

    public ClassroomAgentTask(Guid id, Guid teachingAgentId, Guid teachingAgentVersionId, string title) : base(id)
    {
        TeachingAgentId = teachingAgentId;
        TeachingAgentVersionId = teachingAgentVersionId;
        Title = title;
    }
}
