using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Install;

public interface ILicenseValidator
{
    bool Validate(string licenseKey);
}

/// <summary>
/// 许可证校验。
/// 支持三种模式（按优先级）：
///   1. Install:ValidLicenseKeys 配置的精确密钥白名单；
///   2. Install:LicenseSigningKey 配置的 HMAC 签名许可证（KH-STANDARD-{payload}.{base64url(hmac)}）；
///   3. 未配置任何密钥时回退到旧的前缀校验（仅用于平滑迁移，会记录警告）。
/// 说明：真正阻止匿名安装的是 InstallAccessGuard（安装令牌 / 回环来源），本校验是纵深防御。
/// </summary>
public class FixedLicenseValidator : ILicenseValidator, ITransientDependency
{
    private const string ValidLicensePrefix = "KH-STANDARD-";

    private readonly IConfiguration _configuration;
    private readonly ILogger<FixedLicenseValidator> _logger;

    public FixedLicenseValidator(IConfiguration configuration, ILogger<FixedLicenseValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool Validate(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return false;
        }

        // 1) 精确密钥白名单
        var validKeys = _configuration["Install:ValidLicenseKeys"];
        if (!string.IsNullOrWhiteSpace(validKeys))
        {
            var list = validKeys.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return list.Any(key => FixedTimeEquals(key, licenseKey));
        }

        // 2) HMAC 签名许可证
        var signingKey = _configuration["Install:LicenseSigningKey"];
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            return ValidateSigned(licenseKey, signingKey);
        }

        // 3) 回退：旧前缀校验（未配置密钥时不安全，仅供兼容；匿名安装已由 InstallAccessGuard 拦截）
        _logger.LogWarning(
            "Install:ValidLicenseKeys / Install:LicenseSigningKey 均未配置，许可证仅做前缀校验，建议尽快配置。");
        return licenseKey.StartsWith(ValidLicensePrefix, StringComparison.Ordinal) &&
               licenseKey.Length > ValidLicensePrefix.Length;
    }

    private static bool ValidateSigned(string licenseKey, string signingKey)
    {
        if (!licenseKey.StartsWith(ValidLicensePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payloadWithSignature = licenseKey[ValidLicensePrefix.Length..];
        var separatorIndex = payloadWithSignature.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= payloadWithSignature.Length - 1)
        {
            return false;
        }

        var payload = payloadWithSignature[..separatorIndex];
        var providedSignature = payloadWithSignature[(separatorIndex + 1)..];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var expected = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        return FixedTimeEquals(expected, providedSignature);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
