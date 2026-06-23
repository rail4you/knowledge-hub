using System;
using KnowledgeHub.MicroMajors.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.MicroMajors.Dtos;

public class MicroMajorCertificateDto : FullAuditedEntityDto<Guid>
{
    public Guid MicroMajorId { get; set; }
    public string? MicroMajorTitle { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string CertificateNo { get; set; } = string.Empty;
    public string VerifyCode { get; set; } = string.Empty;
    public string? CertificateImageUrl { get; set; }
    public MicroMajorCertificateStatus Status { get; set; }
    public DateTime IssuedAt { get; set; }
}
