using System;
using KnowledgeHub.Enterprises;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Enterprises.Dtos;

public class EnterpriseProfileDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnifiedSocialCreditCode { get; set; }
    public string? Industry { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Summary { get; set; }
    public string? LogoUrl { get; set; }
    public EnterpriseCertificationStatus CertificationStatus { get; set; }
    public string CertificationStatusName { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    /// <summary>当前是否可写（已认证 + 在有效期内）。</summary>
    public bool IsUsable { get; set; }

    public int MemberCount { get; set; }
}

public class CreateUpdateEnterpriseProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string? UnifiedSocialCreditCode { get; set; }
    public string? Industry { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Summary { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

public class EnterpriseMemberDto
{
    public Guid Id { get; set; }
    public Guid EnterpriseProfileId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public EnterpriseMemberRole Role { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>把某个 EnterpriseUser 账号绑定到企业（学校管理员操作）。</summary>
public class BindEnterpriseMemberDto
{
    public Guid EnterpriseProfileId { get; set; }
    public Guid UserId { get; set; }
    public EnterpriseMemberRole Role { get; set; } = EnterpriseMemberRole.Admin;
    public bool IsPrimary { get; set; } = true;
}

/// <summary>学校有效期（平台侧管理）。</summary>
public class SchoolValidityDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? SchoolName { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsValid { get; set; }
    public bool ReadOnlyAfterExpiry { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public class SetSchoolValidityDto
{
    public Guid TenantId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool ReadOnlyAfterExpiry { get; set; } = true;
}
