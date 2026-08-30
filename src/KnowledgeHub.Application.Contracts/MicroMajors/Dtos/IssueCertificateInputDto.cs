using System;

namespace KnowledgeHub.MicroMajors.Dtos;

public class IssueCertificateInputDto
{
    public Guid EnrollmentId { get; set; }

    /// <summary>所选证书模板 Id（该微专业下预先上传的证书模板），不选则只生成电子证书记录</summary>
    public Guid? CertificateTemplateId { get; set; }

    /// <summary>发证时填写的学号</summary>
    public string? StudentNo { get; set; }

    /// <summary>发证时填写的导师</summary>
    public string? Advisor { get; set; }

    /// <summary>打印在证书上的发证时间</summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>证书有效期截止时间</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>证书编号（手动填写则覆盖自动编号，否则由后端自动生成）</summary>
    public string? CertificateNo { get; set; }

    /// <summary>前端画布合成的最终证书图片 URL（上传 OSS 后得到），优先于模板原图</summary>
    public string? CompositeImageUrl { get; set; }
}