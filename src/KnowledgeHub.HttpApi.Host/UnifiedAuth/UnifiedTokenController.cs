using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.UnifiedAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.Controllers;

namespace KnowledgeHub.Identity;

/// <summary>
/// 覆盖 ABP 的 /connect/token 密码授权：
/// 租户上下文内改为调用统一账户服务校验，成功后由本地 AbpUsers（存在则复用、不存在则 JIT 建号）签发应用令牌。
/// 宿主（平台管理员）保持原 ABP 本地密码流程。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(TokenController))]
public class UnifiedTokenController : TokenController
{
    private readonly IUnifiedAuthService _unified;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<UnifiedTokenController> _logger;

    public UnifiedTokenController(
        IUnifiedAuthService unified,
        ICurrentTenant currentTenant,
        ILogger<UnifiedTokenController> logger)
    {
        _unified = unified;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    protected override async Task<IActionResult> HandlePasswordAsync(OpenIddictRequest request)
    {
        if (!_unified.Enabled || _currentTenant.Id == null)
        {
            return await base.HandlePasswordAsync(request);
        }

        var userName = request.Username?.Trim();
        var password = request.Password;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return InvalidGrant("用户名或密码错误");
        }

        var outcome = await _unified.VerifyAndProvisionAsync(_currentTenant.Id.Value, userName, password);
        if (!outcome.Succeeded || outcome.User == null)
        {
            _logger.LogInformation("统一账户登录失败: tenant={TenantId} user={UserName} reason={Reason}",
                _currentTenant.Id, userName, outcome.Reason);
            return InvalidGrant(outcome.IsNotAllowed ? DescribeReason(outcome.Reason) : "用户名或密码错误");
        }

        return await SetSuccessResultAsync(request, outcome.User);
    }

    private IActionResult InvalidGrant(string description)
    {
        return Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static string DescribeReason(string? reason) => reason switch
    {
        "tenant_expired" or "tenant_not_started" => "学校已过期，请联系管理员续期",
        "membership_expired" or "membership_not_started" => "账号不在有效期内",
        "membership_inactive" or "person_disabled" => "账号已停用",
        "tenant_inactive" => "学校已停用",
        "app_not_entitled" or "app_entitlement_expired" or "app_entitlement_not_started" => "本学校未开通该应用",
        "app_access_disabled" or "app_access_expired" or "app_access_not_started" => "该账号未获得本应用访问权限",
        _ => "当前无法登录，请联系管理员"
    };
}
