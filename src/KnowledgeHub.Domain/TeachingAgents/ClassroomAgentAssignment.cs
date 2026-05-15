using System;
using KnowledgeHub.TeachingAgents.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.TeachingAgents;

public class ClassroomAgentAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ClassroomAgentTaskId { get; set; }
    public Guid StudentId { get; set; }
    public ClassroomAgentAssignmentStatus Status { get; set; } = ClassroomAgentAssignmentStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public string? SubmissionSummary { get; set; }
    public string? HelpReason { get; set; }

    public ClassroomAgentAssignment()
    {
    }

    public ClassroomAgentAssignment(Guid id, Guid classroomAgentTaskId, Guid studentId) : base(id)
    {
        ClassroomAgentTaskId = classroomAgentTaskId;
        StudentId = studentId;
    }
}
