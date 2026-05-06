using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumGuidanceRecord : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid TeacherId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsVisibleToStudent { get; set; } = true;
    public DateTime GuidedAt { get; set; }

    public PracticumGuidanceRecord()
    {
    }

    public PracticumGuidanceRecord(Guid id, Guid projectId, Guid enrollmentId, Guid teacherId, string content)
        : base(id)
    {
        ProjectId = projectId;
        EnrollmentId = enrollmentId;
        TeacherId = teacherId;
        Content = content;
        GuidedAt = DateTime.UtcNow;
    }
}
