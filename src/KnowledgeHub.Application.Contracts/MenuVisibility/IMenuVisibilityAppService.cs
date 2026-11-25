using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.MenuVisibility.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.MenuVisibility;

/// <summary>
/// 按用户（老师）控制侧边栏一级菜单的可见性。
///
/// <para>只有租户管理员（院校管理员 / 宿主管理员）能改别人的配置；
/// 任何登录用户都能读自己的配置（前端启动时据此裁剪侧边栏）。</para>
///
/// <para>注意：这里只管"看不看得见菜单"，不改任何业务权限 ——
/// 老师直接输入网址仍按原权限访问。</para>
/// </summary>
public interface IMenuVisibilityAppService : IApplicationService
{
    /// <summary>当前登录用户被隐藏的一级菜单名（未登录返回空列表）。</summary>
    Task<List<string>> GetMyHiddenMenusAsync();

    /// <summary>本租户（宿主管理员可为指定租户）的老师列表 + 各自已隐藏的一级菜单。</summary>
    Task<PagedResultDto<MenuVisibilityListItemDto>> GetTeachersAsync(GetMenuVisibilityTeachersInput input);

    /// <summary>读取某个用户被隐藏的一级菜单名（租户管理员）。</summary>
    Task<MenuVisibilityDto> GetUserMenusAsync(Guid userId);

    /// <summary>保存某个用户被隐藏的一级菜单名（租户管理员）。</summary>
    Task SetUserMenusAsync(SetMenuVisibilityInput input);

    /// <summary>清空某个用户的菜单限制（恢复全部可见，租户管理员）。</summary>
    Task ResetUserMenusAsync(Guid userId);
}
