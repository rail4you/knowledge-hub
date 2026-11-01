using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Organizations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Filters;

/// <summary>
/// 标记：允许在「单位已过期」的情况下仍然执行（登录、续期、只读自助等）。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowExpiredOrganizationAttribute : Attribute
{
}

/// <summary>
/// 单位有效期门禁（全局 Action 过滤器）。
///
/// <para>
/// 规则（设计方案 7.3 / 决策 C6）：
/// </para>
/// <list type="bullet">
/// <item><description>写操作（POST / PUT / PATCH / DELETE）在单位过期时一律拒绝（业务异常，前端有可读文案）。</description></item>
/// <item><description>读操作（GET / HEAD / OPTIONS）始终放行 —— 已发布内容按原权限展示。</description></item>
/// <item><description>白名单路径放行：登录 / 令牌 / 账号自助 / 本地化 / 健康检查 / ABP 配置接口。</description></item>
/// <item><description>标了 <see cref="AllowExpiredOrganizationAttribute"/> 的控制器 / 方法放行。</description></item>
/// </list>
///
/// <para>
/// 之所以放在 HTTP 层而不是逐个 AppService：一是漏掉一个写接口就是安全洞；
/// 二是这里能按 HttpClient 方法统一判定，新增 Service 自动生效。
/// 后台任务不受本过滤器保护，需在 job 内显式调用 <c>EnsureWritableForTenantAsync</c>。
/// </para>
/// </summary>
public class OrganizationValidityFilter : IAsyncActionFilter, IOrderedFilter
{
    /// <summary>尽量靠后执行：让鉴权等前置过滤器先跑，避免把 401 说成"已过期"。</summary>
    public int Order => 1000;

    private static readonly string[] WriteMethods = { "POST", "PUT", "PATCH", "DELETE" };

    private static readonly string[] AllowPathPrefixes =
    {
        "/connect/",
        "/.well-known/",
        "/api/account/",
        "/api/abp/",
        "/api/feature-management",
        "/api/localization",
        "/health-status",
        "/swagger",
        "/hangfire",
        "/signalr"
    };

    private readonly IOrganizationValidityChecker _checker;
    private readonly ILogger<OrganizationValidityFilter> _logger;

    public OrganizationValidityFilter(
        IOrganizationValidityChecker checker,
        ILogger<OrganizationValidityFilter> logger)
    {
        _checker = checker;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var method = request.Method?.ToUpperInvariant() ?? "GET";

        if (Array.IndexOf(WriteMethods, method) < 0)
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;
        foreach (var prefix in AllowPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }
        }

        // 控制器 / Action 上显式放行
        if (context.ActionDescriptor.EndpointMetadata.Any(m => m is AllowExpiredOrganizationAttribute))
        {
            await next();
            return;
        }

        try
        {
            await _checker.EnsureWritableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[OrgValidity] 拒绝写操作 {Method} {Path}：{Message}", method, path, ex.Message);
            throw;
        }

        await next();
    }
}
