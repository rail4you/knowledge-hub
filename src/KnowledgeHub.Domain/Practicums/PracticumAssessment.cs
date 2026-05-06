using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumAssessment : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid TeacherId { get; set; }
    public decimal Score { get; set; }
    public string? GradeLevel { get; set; }
    public string? Comment { get; set; }
    public string? RubricJson { get; set; }
    public DateTime AssessedAt { get; set; }

    public PracticumAssessment()
    {
    }

    public PracticumAssessment(Guid id, Guid projectId, Guid enrollmentId, Guid teacherId, decimal score)
        : base(id)
    {
        ProjectId = projectId;
        EnrollmentId = enrollmentId;
        TeacherId = teacherId;
        Score = score;
        AssessedAt = DateTime.UtcNow;
    }
}
