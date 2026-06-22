using System.Collections.Generic;

namespace KnowledgeHub.Users;

public class UserImportResultDto
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
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
