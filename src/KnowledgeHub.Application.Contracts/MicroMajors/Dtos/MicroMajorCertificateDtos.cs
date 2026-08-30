using System;
using System.Collections.Generic;
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
    /// <summary>发证时填写的学号</summary>
    public string? StudentNo { get; set; }
    /// <summary>发证时填写的导师</summary>
    public string? Advisor { get; set; }
    /// <summary>打印在证书上的发证时间（中文日期）</summary>
    public DateTime? IssueDate { get; set; }
    /// <summary>证书有效期截止时间</summary>
    public DateTime? ValidUntil { get; set; }
    /// <summary>该证书所用的模板占位符图层</summary>
    public List<CertificateTemplateLayerDto> Layers { get; set; } = new();
}

/// <summary>
/// 发证默认数据：用于打开发证弹窗时自动填充姓名、学号、证书编号等字段。
/// </summary>
public class IssueCertificateDefaultsDto
{
    public Guid EnrollmentId { get; set; }
    public Guid MicroMajorId { get; set; }
    public string? MicroMajorTitle { get; set; }
    public string? StudentName { get; set; }
    public string? StudentNo { get; set; }
    /// <summary>自动生成的候选证书编号，可在发证时被覆盖</summary>
    public string SuggestedCertificateNo { get; set; } = string.Empty;
    /// <summary>建议的发证时间（当天）</summary>
    public DateTime IssueDate { get; set; }
}
