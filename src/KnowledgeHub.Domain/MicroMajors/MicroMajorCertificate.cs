using System;
using KnowledgeHub.MicroMajors.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MicroMajors;

public class MicroMajorCertificate : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid MicroMajorId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string CertificateNo { get; set; } = string.Empty;
    public string VerifyCode { get; set; } = string.Empty;
    public MicroMajorCertificateStatus Status { get; set; } = MicroMajorCertificateStatus.Active;
    public DateTime IssuedAt { get; set; }

    public MicroMajorCertificate()
    {
    }

    public MicroMajorCertificate(Guid id, Guid microMajorId, Guid enrollmentId, Guid studentId, string certificateNo, string verifyCode)
        : base(id)
    {
        MicroMajorId = microMajorId;
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CertificateNo = certificateNo;
        VerifyCode = verifyCode;
        IssuedAt = DateTime.UtcNow;
    }
}
