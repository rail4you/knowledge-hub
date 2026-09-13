using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Install;

public interface IInstallAccessGuard
{
    /// <summary>
    /// 判断当前请求是否允许执行「安装 / 升级」这类高权限初始化操作。
    /// </summary>
    bool IsAccessAllowed(string? providedToken);
}

/// <summary>
/// 安装入口访问控制：
///   - 配置了 Install:Token 时，必须提供完全一致的令牌；
///   - 未配置时，仅允许来自回环地址（服务器本机）的请求。
/// 默认 fail-closed，防止公网匿名调用安装接口抢注管理员。
/// </summary>
public class InstallAccessGuard : IInstallAccessGuard, ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InstallAccessGuard(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAccessAllowed(string? providedToken)
    {
        var configuredToken = _configuration["Install:Token"];

        if (!string.IsNullOrWhiteSpace(configuredToken))
        {
            return !string.IsNullOrWhiteSpace(providedToken) &&
                   FixedTimeEquals(configuredToken, providedToken);
        }

        var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        return remoteIp != null && IPAddress.IsLoopback(remoteIp);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
