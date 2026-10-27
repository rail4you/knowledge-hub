using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Certificates.Dtos;

/// <summary>平台证书模板。</summary>
public class CertificateTemplateDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    /// <summary>证书底图 URL。</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>占位符图层配置（JSON 数组）。</summary>
    public string? LayersJson { get; set; }

    public bool IsDefault { get; set; }

    public bool IsEnabled { get; set; }

    public int SortOrder { get; set; }

    public string? Description { get; set; }
}

/// <summary>创建 / 更新平台证书模板。</summary>
public class CreateUpdateCertificateTemplateDto
{
    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string? LayersJson { get; set; }

    public bool? IsDefault { get; set; }

    public bool? IsEnabled { get; set; }

    public int? SortOrder { get; set; }

    public string? Description { get; set; }
}

/// <summary>结业证书申请。</summary>
public class CertificateApplicationDto : FullAuditedEntityDto<Guid>
{
    public Guid LibraryId { get; set; }

    public string? LibraryName { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid StudentId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string? PhoneMasked { get; set; }

    public string? SchoolName { get; set; }

    public bool IsLearningCompleted { get; set; }

    public bool IsAssessmentPassed { get; set; }

    public bool IsRealNameVerified { get; set; }

    public CertificateApplicationStatus Status { get; set; }

    public DateTime AppliedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewerName { get; set; }

    public string? ReviewRemark { get; set; }

    public Guid? CertificateId { get; set; }

    /// <summary>已发放证书（审核通过时回填，便于前端直接跳转查看）。</summary>
    public CertificateDto? Certificate { get; set; }
}

/// <summary>结业证书。</summary>
public class CertificateDto : FullAuditedEntityDto<Guid>
{
    public Guid TemplateId { get; set; }

    public Guid? ApplicationId { get; set; }

    public Guid LibraryId { get; set; }

    public string? LibraryName { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string? SchoolName { get; set; }

    public string CertificateNo { get; set; } = string.Empty;

    public string VerifyCode { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>结业时间。</summary>
    public DateTime IssueDate { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime? ValidUntil { get; set; }

    public CertificateStatus Status { get; set; }

    /// <summary>是否已归档到主建老师的「其他学员」目录。</summary>
    public bool IsArchived { get; set; }
}

/// <summary>学员申请结业证书。</summary>
public class ApplyCertificateInput
{
    public Guid CourseId { get; set; }

    /// <summary>可选：指定模板（不传则使用默认模板）。</summary>
    public Guid? TemplateId { get; set; }
}

/// <summary>申请资格校验结果（前端用于「能否申请结业证书」提示）。</summary>
public class CertificateApplyEligibilityDto
{
    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid? LibraryId { get; set; }

    public string? LibraryName { get; set; }

    /// <summary>是否已完成学习。</summary>
    public bool IsLearningCompleted { get; set; }

    /// <summary>是否已通过考核。</summary>
    public bool IsAssessmentPassed { get; set; }

    /// <summary>是否已实名认证。</summary>
    public bool IsRealNameVerified { get; set; }

    /// <summary>是否已有待审核 / 已通过的申请。</summary>
    public bool HasApplied { get; set; }

    /// <summary>是否可以申请。</summary>
    public bool CanApply { get; set; }

    /// <summary>不可申请的原因。</summary>
    public string? Reason { get; set; }
}

/// <summary>管理员审核通过并发证。</summary>
public class ApproveCertificateInput
{
    public Guid ApplicationId { get; set; }

    /// <summary>使用的模板 Id（不传用默认模板）。</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>证书编号（不传自动生成）。</summary>
    public string? CertificateNo { get; set; }

    /// <summary>防伪校验码（不传自动生成；发证前可先调 <c>prepare</c> 取得）。</summary>
    public string? VerifyCode { get; set; }

    /// <summary>证书图片 URL（前端画布合成后上传回传）。</summary>
    public string? CertificateImageUrl { get; set; }

    /// <summary>结业时间（不传取当前时间）。</summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>有效期截止。</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>审核备注。</summary>
    public string? ReviewRemark { get; set; }
}

/// <summary>发证前准备：生成证书编号与防伪码（供前端合成证书图片）。</summary>
public class CertificatePrepareDto
{
    public Guid ApplicationId { get; set; }

    public string CertificateNo { get; set; } = string.Empty;

    public string VerifyCode { get; set; } = string.Empty;
}

/// <summary>管理员驳回申请。</summary>
public class RejectCertificateInput
{
    public Guid ApplicationId { get; set; }

    public string? ReviewRemark { get; set; }
}

/// <summary>证书申请分页查询。</summary>
public class GetCertificateApplicationsInput : PagedAndSortedResultRequestDto
{
    /// <summary>按状态过滤（null = 全部）。</summary>
    public CertificateApplicationStatus? Status { get; set; }

    public string? Filter { get; set; }
}

/// <summary>公开防伪核验结果。</summary>
public class CertificateVerifyDto
{
    public bool IsValid { get; set; }

    public string? Message { get; set; }

    public CertificateDto? Certificate { get; set; }
}
