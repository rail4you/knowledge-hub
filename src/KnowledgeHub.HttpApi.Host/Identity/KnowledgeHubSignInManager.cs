using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace KnowledgeHub.Identity;

/// <summary>
/// 全局登录校验兜底：
/// 1. 租户隔离：用户的 TenantId 必须与当前解析到的 <see cref="ICurrentTenant.Id"/> 一致，
///    否则即使密码正确也返回 Failed。防止宿主 admin 在租户上下文（或反之）绕过 EF 租户过滤；
/// 2. admin 保留：用户名为 admin 的宿主全局管理员禁止在任何租户上下文登录。
///    即使租户下历史种子产生了同名的 qidi/admin 账号（TenantId = qidi），也视为保留账号禁止在租户内使用。
/// 该管理器覆盖所有密码登录入口：Account/Login 页面、/connect/token password grant 等。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(AbpSignInManager), typeof(SignInManager<IdentityUser>))]
public class KnowledgeHubSignInManager : AbpSignInManager
{
    private readonly ICurrentTenant _currentTenant;

    public KnowledgeHubSignInManager(
        IdentityUserManager userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<IdentityUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<IdentityUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<IdentityUser> confirmation,
        IOptions<AbpIdentityOptions> abpIdentityOptions,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation, abpIdentityOptions, settingProvider)
    {
        _currentTenant = currentTenant;
    }

    protected override async Task<SignInResult> PreSignInCheck(IdentityUser user)
    {
        var baseResult = await base.PreSignInCheck(user);
        if (!baseResult.Succeeded)
        {
            return baseResult;
        }

        // 保留账号约束：admin 仅允许在宿主（全局）上下文登录。
        if (_currentTenant.Id.HasValue &&
            string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning(
                "KnowledgeHubSignInManager blocked reserved admin login in tenant. TenantId={TenantId}, UserId={UserId}, UserName={UserName}",
                _currentTenant.Id, user.Id, user.UserName);
            return SignInResult.Failed;
        }

        // 租户隔离：用户 TenantId 必须与当前租户一致。
        // 宿主用户 TenantId==null 只能在宿主登录；租户用户 TenantId==Guid 只能在对应租户登录。
        if (user.TenantId != _currentTenant.Id)
        {
            Logger.LogWarning(
                "KnowledgeHubSignInManager blocked cross-tenant login. CurrentTenant={CurrentTenantId}, UserTenantId={UserTenantId}, UserId={UserId}, UserName={UserName}",
                _currentTenant.Id, user.TenantId, user.Id, user.UserName);
            return SignInResult.Failed;
        }

        return SignInResult.Success;
    }
}
