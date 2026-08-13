using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp.Account.Settings;
using Volo.Abp.Account.Web;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Settings;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace KnowledgeHub.Pages.Account;

/// <summary>
/// 登录页模型：
/// 1. 加载时清除残留的已登录会话，避免旧的宿主会话绕过租户校验；
/// 2. 登录失败时不再展示"请确保已执行数据库种子数据"提示，统一显示标准错误信息。
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

    public async override Task<IActionResult> OnGetAsync()
    {
        // 清除残留会话，确保登录页始终要求重新登录校验
        if (CurrentUser.IsAuthenticated)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }

        return await base.OnGetAsync();
    }

    public async override Task<IActionResult> OnPostAsync(string action)
    {
        if (action == "Cancel")
        {
            return await base.OnPostAsync(action);
        }

        await CheckLocalLoginAsync();

        ValidateModel();

        ExternalProviders = await GetExternalProviders();

        EnableLocalLogin = await SettingProvider.IsTrueAsync(AccountSettingNames.EnableLocalLogin);

        await ReplaceEmailToUsernameOfInputIfNeeds();

        await IdentityOptions.SetAsync();

        var result = await SignInManager.PasswordSignInAsync(
            LoginInput.UserNameOrEmailAddress,
            LoginInput.Password,
            LoginInput.RememberMe,
            true
        );

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
            // 不显示"请确保已执行数据库种子数据"提示，统一按用户名或密码错误处理。
            Alerts.Danger(L["InvalidUserNameOrPassword"]);
            return Page();
        }

        IdentityUser user = await FindUserAsync();

        if (user != null)
        {
            // 清除动态声明缓存，避免旧声明残留。
            await IdentityDynamicClaimsPrincipalContributorCache.ClearAsync(user.Id, user.TenantId);
        }

        return await RedirectSafelyAsync(ReturnUrl, ReturnUrlHash);
    }

    private async Task<IdentityUser> FindUserAsync()
    {
        return await UserManager.FindByNameAsync(LoginInput.UserNameOrEmailAddress) ??
               await UserManager.FindByEmailAsync(LoginInput.UserNameOrEmailAddress);
    }
}
