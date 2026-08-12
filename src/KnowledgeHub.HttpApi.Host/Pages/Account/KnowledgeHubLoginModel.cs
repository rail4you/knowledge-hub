using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Volo.Abp.Account.Settings;
using Volo.Abp.Account.Web;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.Settings;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace KnowledgeHub.Pages.Account;

/// <summary>
/// 登录页模型：租户优先、宿主兜底。
///
/// ABP 默认登录按"所选租户"范围校验用户名密码。当用户先选择了某个租户（如 qidi）
/// 再尝试用宿主(全局)管理员 admin/1q2w3E* 登录时，qidi 租户下没有该账号会直接失败。
///
/// 本模型在租户登录失败时回退到宿主(全局)上下文再次尝试登录：
/// - 租户登录成功 → 保持租户身份（本租户管理员登录不受影响）
/// - 租户登录失败、宿主登录成功 → 以宿主(全局)管理员身份登录，并清除 __tenant cookie，
///   保证后续 OpenIddict authorize 流程与令牌的 tenant 上下文一致。
/// </summary>
[ExposeServices(typeof(LoginModel))]
public class KnowledgeHubLoginModel : OpenIddictSupportedLoginModel
{
    public KnowledgeHubLoginModel(
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<AbpAccountOptions> accountOptions,
        IOptions<IdentityOptions> identityOptions,
        IdentityDynamicClaimsPrincipalContributorCache identityDynamicClaimsPrincipalContributorCache,
        AbpOpenIddictRequestHelper openIddictRequestHelper,
        IWebHostEnvironment webHostEnvironment)
        : base(schemeProvider, accountOptions, identityOptions, identityDynamicClaimsPrincipalContributorCache, openIddictRequestHelper, webHostEnvironment)
    {
    }

    public override async Task<IActionResult> OnPostAsync(string action)
    {
        if (action == "Cancel")
        {
            var request = await OpenIddictRequestHelper.GetFromReturnUrlAsync(ReturnUrl);

            var transaction = HttpContext.GetOpenIddictServerTransaction();
            if (request?.ClientId != null && transaction != null)
            {
                transaction.EndpointType = OpenIddictServerEndpointType.Authorization;
                transaction.Request = request;

                var notification = new OpenIddictServerEvents.ValidateAuthorizationRequestContext(transaction);
                transaction.SetProperty(typeof(OpenIddictServerEvents.ValidateAuthorizationRequestContext).FullName!, notification);

                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Redirect("~/");
        }

        await CheckLocalLoginAsync();

        ValidateModel();

        ExternalProviders = await GetExternalProviders();

        EnableLocalLogin = await SettingProvider.IsTrueAsync(AccountSettingNames.EnableLocalLogin);

        await ReplaceEmailToUsernameOfInputIfNeeds();

        await IdentityOptions.SetAsync();

        // 1) 先按所选租户登录（原 ABP 行为）
        var result = await SignInManager.PasswordSignInAsync(
            LoginInput.UserNameOrEmailAddress,
            LoginInput.Password,
            LoginInput.RememberMe,
            true
        );

        IdentityUser loggedInUser = null;

        if (result.Succeeded)
        {
            loggedInUser = await FindUserAsync();
        }
        else if (CurrentTenant.Id != null)
        {
            // 2) 宿主兜底：所选租户中登录失败时，尝试以宿主(全局)身份登录
            using (CurrentTenant.Change(null))
            {
                var hostResult = await SignInManager.PasswordSignInAsync(
                    LoginInput.UserNameOrEmailAddress,
                    LoginInput.Password,
                    LoginInput.RememberMe,
                    true
                );

                if (hostResult.Succeeded)
                {
                    result = hostResult;
                    loggedInUser = await FindUserAsync();

                    // 以宿主身份登录成功后，清除租户 cookie，使后续 authorize/令牌上下文保持宿主。
                    Response.Cookies.Delete(TenantResolverConsts.DefaultTenantKey);
                }
            }
        }

        await IdentitySecurityLogManager.SaveAsync(new IdentitySecurityLogContext()
        {
            Identity = IdentitySecurityLogIdentityConsts.Identity,
            Action = result.ToIdentitySecurityLogAction(),
            UserName = LoginInput.UserNameOrEmailAddress
        });

        if (result.RequiresTwoFactor)
        {
            return await TwoFactorLoginResultAsync();
        }

        if (result.IsLockedOut)
        {
            Alerts.Warning(L["UserLockedOutMessage"]);
            return Page();
        }

        if (result.IsNotAllowed)
        {
            Alerts.Warning(L["LoginIsNotAllowed"]);
            return Page();
        }

        if (!result.Succeeded)
        {
            if (LoginInput.UserNameOrEmailAddress == IdentityDataSeedContributor.AdminUserNameDefaultValue &&
                WebHostEnvironment.IsDevelopment())
            {
                var adminUser = await UserManager.FindByNameAsync(IdentityDataSeedContributor.AdminUserNameDefaultValue);
                if (adminUser == null)
                {
                    ShowRequireMigrateSeedMessage = true;
                    return Page();
                }
            }

            Alerts.Danger(L["InvalidUserNameOrPassword"]);
            return Page();
        }

        if (loggedInUser != null)
        {
            // 清除动态声明缓存，避免旧声明残留。
            await IdentityDynamicClaimsPrincipalContributorCache.ClearAsync(loggedInUser.Id, loggedInUser.TenantId);
        }

        return await RedirectSafelyAsync(ReturnUrl, ReturnUrlHash);
    }

    private async Task<IdentityUser> FindUserAsync()
    {
        return await UserManager.FindByNameAsync(LoginInput.UserNameOrEmailAddress) ??
               await UserManager.FindByEmailAsync(LoginInput.UserNameOrEmailAddress);
    }
}
