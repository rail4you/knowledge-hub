using System;

namespace KnowledgeHub.MicroMajors.Dtos;

public class IssueCertificateInputDto
{
    public Guid EnrollmentId { get; set; }

    /// <summary>所选证书模板 Id（该微专业下预先上传的证书模板），不选则只生成电子证书记录</summary>
    public Guid? CertificateTemplateId { get; set; }
}