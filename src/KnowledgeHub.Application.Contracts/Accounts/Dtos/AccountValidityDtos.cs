using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Accounts;

public class GetAccountValidityListInput : PagedAndSortedResultRequestDto
{
    /// <summary>按用户名 / 姓名 / 展示名模糊检索</summary>
    public string? Filter { get; set; }

    /// <summary>按所属院校（租户）过滤</summary>
    public Guid? TenantId { get; set; }

    /// <summary>按受控角色过滤（SchoolAdmin / Teacher）</summary>
    public string? RoleName { get; set; }

    /// <summary>按状态过滤</summary>
    public AccountValidityStatus? Status { get; set; }

    /// <summary>仅显示 7 天内即将到期或已到期的账号</summary>
    public bool? ExpiringSoon { get; set; }
}

public class SetAccountValidityInput
{
    /// <summary>目标账号 Id</summary>
    public Guid UserId { get; set; }

    /// <summary>有效截止时间；为 null 时结合 <see cref="Permanent"/> 处理。</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>设为永久有效（清空有效期限制并恢复权限）。优先于 ValidUntil。</summary>
    public bool Permanent { get; set; }

    /// <summary>可选：按有效期天数（从现在起 N 天），与 ValidUntil 二选一。</summary>
    public int? Days { get; set; }
}

public class SetAccountValidityBatchInput
{
    /// <summary>目标账号 Id 列表</summary>
    public List<Guid> UserIds { get; set; } = new();

    public DateTime? ValidUntil { get; set; }

    /// <summary>设为永久有效</summary>
    public bool Permanent { get; set; }

    public int? Days { get; set; }
}

public class AccountValidityDto : EntityDto<Guid>
{
    /// <summary>目标账号 Id（ABP IdentityUser.Id）</summary>
    public Guid UserId { get; set; }

    /// <summary>所属院校（租户）Id，null 表示 Host</summary>
    public Guid? TenantId { get; set; }

    /// <summary>所属院校名称</summary>
    public string? TenantName { get; set; }

    /// <summary>账号用户名</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>展示名称</summary>
    public string? DisplayName { get; set; }

    /// <summary>受控角色快照</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>有效截止时间；null 表示永久有效</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>当前状态；null 表示从未设置有效期（永久有效）</summary>
    public AccountValidityStatus? Status { get; set; }

    /// <summary>是否已到期</summary>
    public bool IsExpired { get; set; }

    /// <summary>是否即将到期（7 天内）</summary>
    public bool IsExpiringSoon { get; set; }

    /// <summary>距到期剩余天数（已到期为负数）</summary>
    public int? RemainingDays { get; set; }

    /// <summary>账号本身是否启用</summary>
    public bool UserIsActive { get; set; }

    /// <summary>最近一次操作时间</summary>
    public DateTime? LastModifiedTime { get; set; }
}
