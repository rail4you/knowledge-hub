using System.Net;
using System.Net.Sockets;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire Dashboard 鉴权：
/// - 显式开启公网访问（Hangfire:DashboardAllowPublic=true）时放行（需配合 nginx IP 白名单）
/// - 默认仅放行回环 + Docker 内网（172.16.0.0/12，nginx 代理后 API 看到的是 nginx 容器 IP）
/// - 其他来源拒绝（避免任务详情暴露到公网）
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var configuration = httpContext.RequestServices.GetService<IConfiguration>();

        // 显式开启公网访问（谨慎）
        var allowPublic = configuration?.GetValue("Hangfire:DashboardAllowPublic", false) ?? false;
        if (allowPublic)
        {
            return true;
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return true; // 无 IP（某些代理）保守放行
        }

        // 回环地址：本机 / localhost 直接访问
        if (IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        // 常见内网/私网段（nginx 反代后 API 看到的是网关或代理容器 IP）
        if (IsPrivateNetwork(remoteIp))
        {
            return true;
        }

        return false;
    }

    private static bool IsPrivateNetwork(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        // 10.0.0.0/8
        if (b[0] == 10) return true;
        // 172.16.0.0/12
        if (b[0] == 172 && (b[1] & 0xF0) == 0x10) return true;
        // 192.168.0.0/16
        if (b[0] == 192 && b[1] == 168) return true;
        return false;
    }
}
