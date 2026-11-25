using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.MenuVisibility.Dtos;

/// <summary>某个用户的一级菜单可见性配置。</summary>
public class MenuVisibilityDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    /// <summary>显示名（老师姓名），没有则为空。</summary>
    public string? DisplayName { get; set; }

    /// <summary>被隐藏的一级菜单名列表。</summary>
    public List<string> HiddenMenus { get; set; } = new();
}

/// <summary>保存某个用户的一级菜单可见性。</summary>
public class SetMenuVisibilityInput
{
    public Guid UserId { get; set; }

    /// <summary>要隐藏的一级菜单名列表（传空数组 = 全部可见）。</summary>
    public List<string> HiddenMenus { get; set; } = new();
}

/// <summary>老师列表查询（「菜单可见性管理」页面用）。</summary>
public class GetMenuVisibilityTeachersInput : PagedAndSortedResultRequestDto
{
    /// <summary>按登录名 / 姓名模糊搜索。</summary>
    public string? Filter { get; set; }

    /// <summary>租户 Id（仅宿主管理员有效；租户管理员恒为本租户）。</summary>
    public Guid? TenantId { get; set; }
}

/// <summary>老师列表行：该老师当前被隐藏的一级菜单。</summary>
public class MenuVisibilityListItemDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }

    /// <summary>被隐藏的一级菜单名列表（空 = 全部可见）。</summary>
    public List<string> HiddenMenus { get; set; } = new();

    public int HiddenCount => HiddenMenus.Count;
}
