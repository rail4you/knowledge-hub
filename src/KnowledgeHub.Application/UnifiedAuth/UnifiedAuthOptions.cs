using System;
using System.Collections.Generic;

namespace KnowledgeHub.UnifiedAuth;

/// <summary>
/// 统一账户服务接入配置。生产环境通过环境变量注入，默认关闭，避免未配置时影响现有登录。
/// </summary>
public class UnifiedAuthOptions
{
    /// <summary>是否启用统一账户校验。关闭时完全走本地 ABP Identity 密码。</summary>
    public bool Enabled { get; set; }

    /// <summary>账户服务地址，例如 http://127.0.0.1:8080（内网）。</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

    /// <summary>应用 client_id（在账户服务中登记）。</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>应用 client_secret。</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>HTTP 超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// 账户服务不可用时是否回退到本地密码。默认 false（失败关闭），
    /// 避免统一账户服务故障时出现绕过校验的窗口。
    /// </summary>
    public bool FailOpen { get; set; }

    /// <summary>统一账户角色 → 本应用角色名。</summary>
    public Dictionary<string, string> RoleMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["student"] = "Student",
        ["teacher"] = "Teacher",
        ["admin"] = "SchoolAdmin",
        ["super_admin"] = "SchoolAdmin",
        ["enterprise"] = "EnterpriseUser",
    };

    /// <summary>JIT 建号时的兜底角色。</summary>
    public string DefaultRole { get; set; } = "Student";

    /// <summary>是否允许在登录时自动创建本地用户（JIT 开通）。</summary>
    public bool ProvisionOnLogin { get; set; } = true;

    /// <summary>应用在账户服务中的租户标识是否直接使用 ABP 租户 Id。</summary>
    public bool UseTenantIdAsAppTenantId { get; set; } = true;

    public string ResolveRole(string? unifiedRole)
    {
        if (!string.IsNullOrWhiteSpace(unifiedRole) && RoleMap.TryGetValue(unifiedRole, out var mapped))
        {
            return mapped;
        }
        return DefaultRole;
    }

    public string ResolveUnifiedRole(string? localRole)
    {
        if (!string.IsNullOrWhiteSpace(localRole))
        {
            foreach (var pair in RoleMap)
            {
                if (string.Equals(pair.Value, localRole, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Key;
                }
            }
        }
        return "student";
    }
}
