using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumSubmission : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public int VersionNo { get; set; } = 1;
    public string? Content { get; set; }
    public string? AttachmentUrls { get; set; }
    public string? LinkUrl { get; set; }
    public string? ScreenshotUrls { get; set; }
    public PracticumSubmissionStatus Status { get; set; } = PracticumSubmissionStatus.Submitted;
    public DateTime SubmittedAt { get; set; }
    public string? TeacherFeedback { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public decimal? Score { get; set; }

    public PracticumSubmission()
    {
    }

    public PracticumSubmission(Guid id, Guid projectId, Guid taskId, Guid enrollmentId, Guid studentId)
        : base(id)
    {
        ProjectId = projectId;
        TaskId = taskId;
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        SubmittedAt = DateTime.UtcNow;
    }
}
