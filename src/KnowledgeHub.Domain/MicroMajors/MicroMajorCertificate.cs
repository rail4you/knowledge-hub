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
    /// <summary>证书图片 URL（发证时由前端画布合成后上传得到）</summary>
    public string? CertificateImageUrl { get; set; }
    public MicroMajorCertificateStatus Status { get; set; } = MicroMajorCertificateStatus.Active;
    public DateTime IssuedAt { get; set; }
    /// <summary>发证时填写的学号</summary>
    public string? StudentNo { get; set; }
    /// <summary>发证时填写的导师</summary>
    public string? Advisor { get; set; }
    /// <summary>打印在证书上的发证时间（中文日期）</summary>
    public DateTime? IssueDate { get; set; }
    /// <summary>证书有效期截止时间</summary>
    public DateTime? ValidUntil { get; set; }

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
