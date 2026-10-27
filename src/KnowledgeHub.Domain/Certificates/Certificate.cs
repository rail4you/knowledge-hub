using System;
using KnowledgeHub.Certificates;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Certificates;

/// <summary>
/// 已发放的电子结业证书。
///
/// <para>
/// PRD 第 9 节：
/// </para>
/// <list type="bullet">
/// <item><description>证书模板由平台统一配置，证书字段含：校徽位、学员姓名、课程名称、结业时间、证书编号、防伪二维码。</description></item>
/// <item><description>证书发放记录归入<b>主建老师账号</b>下「其他学员」目录中对应学员的档案。</description></item>
/// </list>
/// </summary>
public class Certificate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxCertificateNoLength = 64;
    public const int MaxVerifyCodeLength = 64;
    public const int MaxStudentNameLength = 128;
    public const int MaxCourseTitleLength = 256;
    public const int MaxSchoolNameLength = 256;
    public const int MaxLibraryNameLength = 256;
    public const int MaxImageUrlLength = 1000;

    /// <summary>使用的模板 Id。</summary>
    public Guid TemplateId { get; set; }

    /// <summary>来源申请 Id。</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>所属资源库 Id。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>课程 Id。</summary>
    public Guid CourseId { get; set; }

    /// <summary>学员用户 Id。</summary>
    public Guid StudentId { get; set; }

    /// <summary>学员姓名快照（印在证书上）。</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>课程名称快照。</summary>
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>主建学校名称快照。</summary>
    public string? SchoolName { get; set; }

    /// <summary>资源库名称快照。</summary>
    public string? LibraryName { get; set; }

    /// <summary>证书编号。</summary>
    public string CertificateNo { get; set; } = string.Empty;

    /// <summary>防伪校验码（二维码内容指向公开核验页）。</summary>
    public string VerifyCode { get; set; } = string.Empty;

    /// <summary>证书图片 URL（前端画布合成后上传）。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>结业时间（印在证书上）。</summary>
    public DateTime IssueDate { get; set; }

    /// <summary>发证时间。</summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>证书有效期截止（可空）。</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>证书状态。</summary>
    public CertificateStatus Status { get; set; } = CertificateStatus.Active;

    /// <summary>主建老师（归档到该账号的「其他学员」目录）。</summary>
    public Guid? OwnerTeacherId { get; set; }

    /// <summary>其他学员档案 Id。</summary>
    public Guid? LearnerId { get; set; }

    /// <summary>数据归属租户（= 主建学校）。</summary>
    public Guid? TenantId { get; set; }

    public Certificate()
    {
    }

    public Certificate(
        Guid id,
        Guid templateId,
        Guid libraryId,
        Guid courseId,
        Guid studentId,
        string studentName,
        string courseTitle,
        string certificateNo,
        string verifyCode,
        Guid? ownerTenantId)
        : base(id)
    {
        TemplateId = templateId;
        LibraryId = libraryId;
        CourseId = courseId;
        StudentId = studentId;
        StudentName = studentName;
        CourseTitle = courseTitle;
        CertificateNo = certificateNo;
        VerifyCode = verifyCode;
        TenantId = ownerTenantId;
        IssuedAt = DateTime.UtcNow;
        IssueDate = DateTime.UtcNow;
    }

    public void SetImageUrl(string? imageUrl)
    {
        ImageUrl = imageUrl;
    }

    public void SetValidUntil(DateTime? validUntil)
    {
        ValidUntil = validUntil;
    }

    public void SetStatus(CertificateStatus status)
    {
        Status = status;
    }

    public void SetApplicationId(Guid applicationId)
    {
        ApplicationId = applicationId;
    }

    public void SetArchive(Guid? ownerTeacherId, Guid? learnerId)
    {
        OwnerTeacherId = ownerTeacherId;
        LearnerId = learnerId;
    }

    public void SetSnapshots(string? schoolName, string? libraryName)
    {
        SchoolName = schoolName;
        LibraryName = libraryName;
    }
}
