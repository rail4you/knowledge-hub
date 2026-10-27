using System;
using KnowledgeHub.Certificates;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Certificates;

/// <summary>
/// 结业证书申请 —— PRD 第 9 节「审核发放」：其他学员完成学习并通过考核后，
/// 在个人端主动申请结业证书；申请提交后由<b>平台管理员</b>统一审核，审核通过后平台发放电子证书。
/// </summary>
public class CertificateApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxApplicantNameLength = 128;
    public const int MaxCourseTitleLength = 256;
    public const int MaxRemarkLength = 500;

    /// <summary>所属资源库 Id（共享库 / 共建库）。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>课程 Id。</summary>
    public Guid CourseId { get; set; }

    /// <summary>申请学员 Id。</summary>
    public Guid StudentId { get; set; }

    /// <summary>学员姓名快照。</summary>
    public string ApplicantName { get; set; } = string.Empty;

    /// <summary>课程名称快照。</summary>
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>资源库名称快照。</summary>
    public string? LibraryName { get; set; }

    /// <summary>主建学校名称快照（印在证书上）。</summary>
    public string? SchoolName { get; set; }

    /// <summary>学员手机号快照（脱敏）。</summary>
    public string? PhoneMasked { get; set; }

    /// <summary>学员是否已完成实名认证。</summary>
    public bool IsRealNameVerified { get; set; }

    /// <summary>是否已通过考核。</summary>
    public bool IsAssessmentPassed { get; set; }

    /// <summary>是否已完成学习。</summary>
    public bool IsLearningCompleted { get; set; }

    /// <summary>主建老师（证书归档到该账号的「其他学员」目录）。</summary>
    public Guid? OwnerTeacherId { get; set; }

    /// <summary>其他学员档案 Id（归档到主建老师「其他学员」目录中的对应学员档案）。</summary>
    public Guid? LearnerId { get; set; }

    /// <summary>申请状态。</summary>
    public CertificateApplicationStatus Status { get; set; } = CertificateApplicationStatus.Pending;

    /// <summary>申请时间。</summary>
    public DateTime AppliedAt { get; set; }

    /// <summary>审核时间。</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>审核人用户 Id。</summary>
    public Guid? ReviewerUserId { get; set; }

    /// <summary>审核人姓名。</summary>
    public string? ReviewerName { get; set; }

    /// <summary>审核意见。</summary>
    public string? ReviewRemark { get; set; }

    /// <summary>发放的证书 Id（审核通过后回填）。</summary>
    public Guid? CertificateId { get; set; }

    /// <summary>数据归属租户（= 主建学校）。</summary>
    public Guid? TenantId { get; set; }

    public CertificateApplication()
    {
    }

    public CertificateApplication(
        Guid id,
        Guid libraryId,
        Guid courseId,
        Guid studentId,
        string applicantName,
        string courseTitle,
        Guid? ownerTenantId)
        : base(id)
    {
        LibraryId = libraryId;
        CourseId = courseId;
        StudentId = studentId;
        ApplicantName = applicantName;
        CourseTitle = courseTitle;
        TenantId = ownerTenantId;
        Status = CertificateApplicationStatus.Pending;
        AppliedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewerUserId, string? reviewerName, string? remark, Guid certificateId, DateTime time)
    {
        Status = CertificateApplicationStatus.Approved;
        ReviewerUserId = reviewerUserId;
        ReviewerName = reviewerName;
        ReviewRemark = remark;
        CertificateId = certificateId;
        ReviewedAt = time;
    }

    public void Reject(Guid reviewerUserId, string? reviewerName, string? remark, DateTime time)
    {
        Status = CertificateApplicationStatus.Rejected;
        ReviewerUserId = reviewerUserId;
        ReviewerName = reviewerName;
        ReviewRemark = remark;
        ReviewedAt = time;
    }

    public void Cancel()
    {
        Status = CertificateApplicationStatus.Cancelled;
    }
}
