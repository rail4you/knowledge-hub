using System;
using KnowledgeHub.MicroMajors.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MicroMajors;

public class MicroMajorEnrollment : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid MicroMajorId { get; set; }
    public Guid StudentId { get; set; }
    public MicroMajorEnrollmentStatus Status { get; set; } = MicroMajorEnrollmentStatus.Pending;
    public decimal Progress { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }

    public MicroMajorEnrollment()
    {
    }

    public MicroMajorEnrollment(Guid id, Guid microMajorId, Guid studentId)
        : base(id)
    {
        MicroMajorId = microMajorId;
        StudentId = studentId;
        EnrolledAt = DateTime.UtcNow;
    }
}
