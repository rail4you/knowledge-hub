using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KnowledgeHub.Install;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace KnowledgeHub.Install;

public class FixedLicenseValidatorTests
{
    private static FixedLicenseValidator Create(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();

        return new FixedLicenseValidator(config, NullLogger<FixedLicenseValidator>.Instance);
    }

    [Fact]
    public void Empty_Key_Should_Be_Rejected()
    {
        Create().Validate("").ShouldBeFalse();
        Create().Validate("   ").ShouldBeFalse();
    }

    [Fact]
    public void Without_Config_Should_Fall_Back_To_Prefix()
    {
        var validator = Create();
        validator.Validate("KH-STANDARD-anything").ShouldBeTrue();
        validator.Validate("OTHER-anything").ShouldBeFalse();
    }

    [Fact]
    public void Exact_Key_Whitelist_Should_Only_Accept_Configured_Keys()
    {
        var validator = Create(("Install:ValidLicenseKeys", "KH-STANDARD-A,KH-STANDARD-B"));

        validator.Validate("KH-STANDARD-A").ShouldBeTrue();
        validator.Validate("KH-STANDARD-B").ShouldBeTrue();
        validator.Validate("KH-STANDARD-C").ShouldBeFalse();
        validator.Validate("KH-STANDARD-").ShouldBeFalse();
    }

    [Fact]
    public void Signed_License_Should_Verify_Hmac()
    {
        const string signingKey = "test-signing-key";
        var validator = Create(("Install:LicenseSigningKey", signingKey));

        var valid = Sign(signingKey, "2024-FREE");
        validator.Validate(valid).ShouldBeTrue();

        // 篡改 payload -> 无效
        validator.Validate("KH-STANDARD-2024-PAID." + valid.Split('.')[1]).ShouldBeFalse();
        // 无签名 -> 无效
        validator.Validate("KH-STANDARD-2024-FREE").ShouldBeFalse();
        validator.Validate("KH-STANDARD-2024-FREE.wrong-signature").ShouldBeFalse();
    }

    private static string Sign(string signingKey, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var signature = System.Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"KH-STANDARD-{payload}.{signature}";
    }
}
