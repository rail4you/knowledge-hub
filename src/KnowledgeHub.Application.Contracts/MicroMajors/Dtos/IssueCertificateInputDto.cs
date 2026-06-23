using System;

namespace KnowledgeHub.MicroMajors.Dtos;

public class IssueCertificateInputDto
{
    public Guid EnrollmentId { get; set; }

    /// <summary>发证时上传的证书图片 URL（由 OSS 上传后得到）</summary>
    public string? CertificateImageUrl { get; set; }
}
