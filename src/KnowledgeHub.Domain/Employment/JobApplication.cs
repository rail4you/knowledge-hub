using System;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class JobApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ResumeId { get; set; }
    public string? CoverLetter { get; set; }
    public EmploymentApplicationStatus Status { get; set; } = EmploymentApplicationStatus.Submitted;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? EmployerRemark { get; set; }

    public JobApplication()
    {
    }

    public JobApplication(Guid id, Guid jobPostingId, Guid studentId, Guid resumeId)
        : base(id)
    {
        JobPostingId = jobPostingId;
        StudentId = studentId;
        ResumeId = resumeId;
    }
}
