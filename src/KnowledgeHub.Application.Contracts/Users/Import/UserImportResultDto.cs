using System.Collections.Generic;

namespace KnowledgeHub.Users;

/// <summary>
/// 单行导入的处理结果状态。
/// 注意命名：用作预览时表示"将会做"什么，用作实际导入时表示"实际做"了什么。
/// </summary>
public enum UserImportItemStatus
{
    /// <summary>将新建用户</summary>
    New = 0,
    /// <summary>将覆盖现有用户</summary>
    Overwrite = 1,
    /// <summary>跳过（如：用户名重复且未开启覆盖、租户内分配全局角色等）</summary>
    Skip = 2,
    /// <summary>校验失败，该行不会写入</summary>
    Fail = 3,
}

/// <summary>
/// 单行导入预览/结果（前端表格里每一行的数据）。
/// 同一结构同时用于 PreviewImportAsync 返回值与 ImportAsync 返回值；
/// 预览阶段 Status 表示"将会做"什么，导入阶段 Status 表示"实际做"了什么。
/// </summary>
public class UserImportPreviewItemDto
{
    /// <summary>Excel 中所在的物理行号（含标题/说明/表头偏移，便于定位）</summary>
    public int RowNumber { get; set; }

    /// <summary>角色类型</summary>
    public UserRoleType RoleType { get; set; }

    /// <summary>角色类型中文名（用于前端直接展示）</summary>
    public string RoleDisplayName { get; set; } = string.Empty;

    /// <summary>登录账号（解析失败时为空）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>所属租户名称（host 管理员导入并指定租户时填写）</summary>
    public string? TenantName { get; set; }

    /// <summary>处理状态</summary>
    public UserImportItemStatus Status { get; set; }

    /// <summary>失败/跳过原因（仅 Status=Skip|Fail 时填写）</summary>
    public string? Reason { get; set; }

    /// <summary>已存在用户的 ID（仅 Status=Overwrite 时填写，前端用于二次确认）</summary>
    public string? ExistingUserId { get; set; }
}

public class UserImportResultDto
{
    /// <summary>实际参与处理的有效行数（不含空行、解析失败前的空读）</summary>
    public int TotalCount { get; set; }

    /// <summary>将新建/已新建的用户数</summary>
    public int NewCount { get; set; }

    /// <summary>将覆盖/已覆盖的现有用户数</summary>
    public int OverwriteCount { get; set; }

    /// <summary>跳过的行数（用户名重复且未开启覆盖、租户冲突等）</summary>
    public int SkipCount { get; set; }

    /// <summary>失败的行数（必填项缺失、格式错误等）</summary>
    public int FailCount { get; set; }

    /// <summary>每行的详细处理结果，前端表格直接渲染</summary>
    public List<UserImportPreviewItemDto> Items { get; set; } = new();

    /// <summary>兼容旧前端：仅含失败行的精简列表</summary>
    public List<UserImportFailItemDto> FailItems { get; set; } = new();
}

public class UserImportFailItemDto
{
    public int RowNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>P1-2：角色 → 已授予权限概要（用于核对 SchoolAdmin vs LeagueAdmin 等角色差异）</summary>
public class RolePermissionSummaryDto
{
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int GrantedPermissionCount { get; set; }
    /// <summary>租户范围（true=全局；false=仅本校）</summary>
    public bool IsGlobal { get; set; }
    /// <summary>关键权限标记（用于区分 LeagueAdmin 独有权限）</summary>
    public List<string> HighlightPermissions { get; set; } = new();
}
