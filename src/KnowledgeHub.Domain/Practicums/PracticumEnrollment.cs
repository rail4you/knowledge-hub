using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumEnrollment : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid StudentId { get; set; }
    public PracticumEnrollmentStatus Status { get; set; } = PracticumEnrollmentStatus.Enrolled;
    public decimal Progress { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastSubmittedAt { get; set; }
    public decimal? FinalScore { get; set; }
    public string? FinalComment { get; set; }
    public DateTime? CompletedAt { get; set; }

    public PracticumEnrollment()
    {
    }

    public PracticumEnrollment(Guid id, Guid projectId, Guid studentId)
        : base(id)
    {
        ProjectId = projectId;
        StudentId = studentId;
        EnrolledAt = DateTime.UtcNow;
    }
}
