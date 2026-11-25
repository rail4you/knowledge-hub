using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MenuVisibility;

/// <summary>
/// 用户级「侧边栏一级菜单可见性」配置（一个用户最多一行）。
///
/// <para>存的是"被隐藏"的一级菜单名（路由节点名，如 <c>::Menu:Employment</c>），
/// 未出现在列表里的菜单默认可见 —— 这样新加的菜单天然对所有人可见，
/// 不需要给每个用户补一行配置。</para>
///
/// <para>只影响导航外观：不改动任何权限授权，老师直接输网址仍可按原有权限访问。</para>
/// </summary>
public class UserMenuVisibility : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>菜单名最大长度（路由节点名，如 ::Menu:Employment）。</summary>
    public const int MaxMenuNameLength = 128;

    /// <summary>单用户最多可隐藏的菜单条数（防御性上限）。</summary>
    public const int MaxHiddenMenuCount = 200;

    public Guid? TenantId { get; set; }

    /// <summary>被配置的用户（老师）。</summary>
    public Guid UserId { get; set; }

    /// <summary>被隐藏的一级菜单名，JSON 字符串数组。</summary>
    public string HiddenMenus { get; set; } = "[]";

    private UserMenuVisibility()
    {
    }

    public UserMenuVisibility(Guid id, Guid userId, Guid? tenantId) : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
    }

    /// <summary>读取隐藏菜单名列表（脏数据/空值统一降级为空列表）。</summary>
    public List<string> GetHiddenMenus()
    {
        if (string.IsNullOrWhiteSpace(HiddenMenus))
        {
            return new List<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(HiddenMenus);
            return list == null
                ? new List<string>()
                : Normalize(list);
        }
        catch (JsonException)
        {
            // 历史脏数据不阻断登录/渲染：当作"没有隐藏任何菜单"。
            return new List<string>();
        }
    }

    public void SetHiddenMenus(IEnumerable<string>? menuNames)
    {
        HiddenMenus = JsonSerializer.Serialize(Normalize(menuNames));
    }

    /// <summary>去空、去重、截断超长名称，并按名称排序保证幂等（相同输入 → 相同 JSON）。</summary>
    private static List<string> Normalize(IEnumerable<string>? menuNames)
    {
        if (menuNames == null)
        {
            return new List<string>();
        }

        return menuNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Select(x => x.Length > MaxMenuNameLength ? x[..MaxMenuNameLength] : x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Take(MaxHiddenMenuCount)
            .ToList();
    }
}
