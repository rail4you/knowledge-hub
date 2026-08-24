using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Accounts;

/// <summary>
/// 账号有效期配置（多校协同 - 账号生命周期管控）
///
/// 全局管理员可为「租户管理员/SchoolAdmin」「教师/Teacher」按用户配置有效期限。
/// 到期后系统自动收回该角色下的编辑/管理类权限（保留只读），续期后一键恢复。
///
/// 该实体不实现 IMultiTenant：由全局管理员在 host 上下文统一维护所有院校账号，
/// 所属租户以 <see cref="TenantId"/> 字段显式记录（null 表示正在配置时所在租户无。
/// 实际上只有租户用户才需要有效期管控，因此 TenantId 通常情况下有值）。
/// </summary>
public class AccountValidity : FullAuditedAggregateRoot<Guid>
{
    public const int MaxUserNameLength = 256;
    public const int MaxDisplayNameLength = 256;
    public const int MaxRoleNameLength = 128;

    /// <summary>目标账号 Id（ABP IdentityUser.Id）</summary>
    public Guid UserId { get; set; }

    /// <summary>目标账号所属租户（院校）。null 表示宿主级账号（一般不参与有效期管控）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>账号用户名快照（便于后台展示与检索）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>展示名称快照</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 快照的受控角色（SchoolAdmin / Teacher）。
    /// 到期时按该角色收回「编辑/管理」权限；续期时按该角色恢复。
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>有效截止时间。null 表示永久有效（不限制）。</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>当前状态</summary>
    public AccountValidityStatus Status { get; set; }

    /// <summary>
    /// 到期时被收回的权限名列表（JSON 数组字符串）。
    /// 用于续期恢复时精确还原被熔断的权限。
    /// </summary>
    public string? RevokedPermissionsJson { get; set; }

    /// <summary>最近一次到期时间</summary>
    public DateTime? ExpiredAt { get; set; }

    /// <summary>最近一次恢复（续期）时间</summary>
    public DateTime? RestoredAt { get; set; }

    protected AccountValidity() { }

    public AccountValidity(
        Guid id,
        Guid userId,
        Guid? tenantId,
        string userName,
        string displayName,
        string roleName,
        DateTime? validUntil)
        : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        UserName = userName;
        DisplayName = displayName;
        RoleName = roleName;
        ValidUntil = validUntil;
        Status = AccountValidityStatus.Active;
    }

    /// <summary>更新有效截止时间（未立即触发状态判断，由调用方执行权限同步）</summary>
    public void UpdateValidUntil(DateTime? validUntil)
    {
        ValidUntil = validUntil;
    }

    /// <summary>标记为已到期（到期触发后调用）</summary>
    public void MarkExpired(string? revokedPermissionsJson, DateTime now)
    {
        Status = AccountValidityStatus.Expired;
        RevokedPermissionsJson = revokedPermissionsJson;
        ExpiredAt = now;
        RestoredAt = null;
    }

    /// <summary>标记为已恢复（续期恢复后调用）</summary>
    public void MarkRestored(DateTime now)
    {
        Status = AccountValidityStatus.Active;
        RevokedPermissionsJson = null;
        RestoredAt = now;
    }
}
