using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace KnowledgeHub.Install;

public class InstallAccessGuardTests
{
    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static IConfiguration Config(params (string Key, string Value)[] settings)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();

    private static InstallAccessGuard Create(string? token, IPAddress? remoteIp)
    {
        var settings = token == null
            ? new (string, string)[0]
            : new[] { ("Install:Token", token) };

        var accessor = new FakeHttpContextAccessor();
        if (remoteIp != null)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = remoteIp;
            accessor.HttpContext = ctx;
        }

        return new InstallAccessGuard(Config(settings), accessor);
    }

    [Fact]
    public void With_Token_Configured_Should_Require_Exact_Match()
    {
        var guard = Create("s3cret", IPAddress.Loopback);

        guard.IsAccessAllowed("s3cret").ShouldBeTrue();
        guard.IsAccessAllowed("wrong").ShouldBeFalse();
        guard.IsAccessAllowed("").ShouldBeFalse();
        guard.IsAccessAllowed(null).ShouldBeFalse();
    }

    [Fact]
    public void Without_Token_Should_Only_Allow_Loopback()
    {
        Create(null, IPAddress.Loopback).IsAccessAllowed(null).ShouldBeTrue();
        Create(null, IPAddress.IPv6Loopback).IsAccessAllowed(null).ShouldBeTrue();
        Create(null, IPAddress.Parse("8.8.8.8")).IsAccessAllowed(null).ShouldBeFalse();
        // 无 HttpContext（后台任务等）fail-closed
        Create(null, null).IsAccessAllowed(null).ShouldBeFalse();
    }
}
