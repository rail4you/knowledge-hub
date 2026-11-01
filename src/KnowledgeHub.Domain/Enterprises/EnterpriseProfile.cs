using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Enterprises;

/// <summary>企业认证状态。</summary>
public enum EnterpriseCertificationStatus : byte
{
    /// <summary>草稿：资料未提交。</summary>
    Draft = 0,

    /// <summary>待审核：已提交，等合作校初审 / 联盟终审。</summary>
    PendingReview = 1,

    /// <summary>已认证：通过审核，可发布岗位、投合作需求。</summary>
    Certified = 2,

    /// <summary>已驳回：需修改后重提。</summary>
    Rejected = 3,

    /// <summary>已冻结：到期 / 违规，禁止写操作但保留历史。</summary>
    Suspended = 4
}

/// <summary>企业成员角色。</summary>
public enum EnterpriseMemberRole : byte
{
    /// <summary>企业管理员：可维护企业资料、岗位、合作需求。</summary>
    Admin = 0,

    /// <summary>普通成员：仅可查看与企业相关的授权内容。</summary>
    Member = 1
}

/// <summary>
/// 企业主体（产教合作 / 企业工作台的业务归属单位）。
///
/// <para>
/// <b>与租户的关系</b>：本系统「租户 = 学校」，企业用户是某学校租户下的
/// <c>EnterpriseUser</c> 角色账号。企业的业务归属由本实体决定，<b>不再用租户 Id 当企业身份</b>。
/// 一个租户下可以有多个企业（例如某校引入的多个合作企业）。
/// </para>
///
/// <para>
/// <b>认证链路</b>：入驻校初审 → 联盟终审（走统一审核单 <c>ReviewTargetType.EnterpriseProfile</c>）。
/// 认证通过前不允许发布岗位 / 投递合作需求。
/// </para>
/// </summary>
public class EnterpriseProfile : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxNameLength = 256;
    public const int MaxCreditCodeLength = 32;
    public const int MaxIndustryLength = 128;
    public const int MaxContactNameLength = 128;
    public const int MaxContactPhoneLength = 64;
    public const int MaxContactEmailLength = 256;
    public const int MaxSummaryLength = 4000;
    public const int MaxLogoUrlLength = 1000;

    /// <summary>入驻学校（租户）。企业账号挂在哪个学校的租户下。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>企业名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>统一社会信用代码。</summary>
    public string? UnifiedSocialCreditCode { get; set; }

    public string? Industry { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public string? Summary { get; set; }

    public string? LogoUrl { get; set; }

    public EnterpriseCertificationStatus CertificationStatus { get; set; } = EnterpriseCertificationStatus.Draft;

    /// <summary>认证审核人（最后一次）。</summary>
    public Guid? CertifierUserId { get; set; }

    public DateTime? CertifiedAt { get; set; }

    /// <summary>最近一次驳回原因。</summary>
    public string? RejectReason { get; set; }

    /// <summary>企业负责人（创建人）。</summary>
    public Guid? OwnerUserId { get; set; }

    // ── 单位有效期（企业侧） ──

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public bool SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MaxNameLength);
        return true;
    }

    public void SetValidity(DateTime? validFrom, DateTime? validTo)
    {
        if (validFrom.HasValue && validTo.HasValue && validTo.Value < validFrom.Value)
        {
            throw new UserFriendlyException("有效期截止时间不能早于开始时间。");
        }

        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>当前是否在有效期内（未设置 ValidTo 视为长期有效）。</summary>
    public bool IsValidAt(DateTime now)
    {
        if (ValidFrom.HasValue && now < ValidFrom.Value)
        {
            return false;
        }

        return !ValidTo.HasValue || now < ValidTo.Value.Date.AddDays(1);
    }

    public void MarkPendingReview()
    {
        CertificationStatus = EnterpriseCertificationStatus.PendingReview;
        RejectReason = null;
    }

    public void MarkCertified(Guid? certifierId)
    {
        CertificationStatus = EnterpriseCertificationStatus.Certified;
        CertifierUserId = certifierId;
        CertifiedAt = DateTime.UtcNow;
        RejectReason = null;
    }

    public void MarkRejected(string? reason)
    {
        CertificationStatus = EnterpriseCertificationStatus.Rejected;
        RejectReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim().Length > 500 ? reason.Trim()[..500] : reason.Trim();
    }

    /// <summary>可用状态：已认证且在有效期内。</summary>
    public bool IsUsable(DateTime now)
    {
        return CertificationStatus == EnterpriseCertificationStatus.Certified && IsValidAt(now);
    }
}

/// <summary>
/// 企业成员（用户 ↔ 企业）。
///
/// <para>企业用户的<b>业务归属</b>由本表决定；<c>EnterpriseUser</c> 角色只表示"这是企业侧账号"。</para>
/// </summary>
public class EnterpriseMember : CreationAuditedEntity<Guid>, IMultiTenant
{
    public EnterpriseMember()
    {
    }

    public EnterpriseMember(Guid id)
        : base(id)
    {
    }

    public Guid EnterpriseProfileId { get; set; }

    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public EnterpriseMemberRole Role { get; set; } = EnterpriseMemberRole.Admin;

    /// <summary>是否主联系人（一个企业可有多个账号，主联系人只有一个）。</summary>
    public bool IsPrimary { get; set; }

    /// <summary>用户所属学校租户（与企业的入驻校一致）。</summary>
    public Guid? TenantId { get; set; }
}
