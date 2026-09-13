using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace KnowledgeHub.Controllers;

/// <summary>
/// 代理类接口（HTTP 代理 / 图片代理）的 SSRF 防护工具：
///   - 配置了 allowlist 时仅精确放行白名单 host；
///   - 未配置时仅允许解析到公网地址的 host，阻断回环/私有/链路本地/保留地址。
/// </summary>
internal static class ProxyHostGuard
{
    public static bool IsHostAllowed(string hostWithOptionalPort, string? allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(hostWithOptionalPort))
        {
            return false;
        }

        var hostname = ExtractHostName(hostWithOptionalPort);
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(allowedHosts))
        {
            var list = allowedHosts.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return list.Any(x =>
                string.Equals(x, hostWithOptionalPort, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, hostname, StringComparison.OrdinalIgnoreCase));
        }

        if (hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            hostname.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            hostname.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(hostname, out var ip))
        {
            return IsPublicIp(ip);
        }

        try
        {
            var addresses = Dns.GetHostAddresses(hostname);
            return addresses.Length > 0 && addresses.All(IsPublicIp);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static string ExtractHostName(string host)
    {
        var value = host.Trim();

        // 丢弃 userinfo（避免 user@host 形式的解析差异）
        var at = value.IndexOf('@');
        if (at >= 0)
        {
            value = value[(at + 1)..];
        }

        // IPv6 字面量 [::1]:8003 / [::1]
        if (value.StartsWith('['))
        {
            var end = value.IndexOf(']');
            return end < 0 ? string.Empty : value[1..end];
        }

        var colon = value.LastIndexOf(':');
        if (colon > 0)
        {
            value = value[..colon];
        }

        return value.Trim();
    }

    public static bool IsPublicIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6None))
            {
                return false;
            }
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            {
                return false;
            }

            // 唯一本地地址 fc00::/7
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return false;
            }
            return true;
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        if (b[0] == 0) return false;                                        // 0.0.0.0/8
        if (b[0] == 10) return false;                                       // 10.0.0.0/8
        if (b[0] == 127) return false;                                      // 127.0.0.0/8
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;          // 172.16.0.0/12
        if (b[0] == 192 && b[1] == 168) return false;                       // 192.168.0.0/16
        if (b[0] == 169 && b[1] == 254) return false;                       // 169.254.0.0/16
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;         // 100.64.0.0/10 CGNAT
        if (b[0] >= 224) return false;                                      // 组播/保留

        return true;
    }
}
