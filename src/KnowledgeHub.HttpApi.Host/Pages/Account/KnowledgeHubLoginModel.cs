using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

        // 租户隔离预校验：全局 admin（TenantId=null 宿主用户）禁止在租户上下文登录。
        // 若用户名为 admin 且当前为租户上下文，直接视为用户名或密码错误，避免泄露账号存在性。
        // ABP 的底层仓储按租户过滤：宿主上下文只看到 TenantId=null 的用户，租户上下文只看到本租户用户；
        // 但当租户下也存在同名 admin（如历史种子产生的 qidi/admin），PasswordSignInAsync 会命中租户 admin 而非宿主 admin，
        // 仍会在租户 tab 下登录成功。为落实“admin 仅宿主可用”的产品约束，租户上下文下禁止任何 admin 登录。
        if (CurrentTenant.Id.HasValue &&
            string.Equals(LoginInput.UserNameOrEmailAddress?.Trim(), "admin", System.StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Blocked host admin login attempt in tenant context. TenantId={TenantId}, UserName={UserName}", CurrentTenant.Id, LoginInput.UserNameOrEmailAddress);
            Alerts.Danger(L["InvalidUserNameOrPassword"]);
            return Page();
        }

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
            // 租户隔离后校验：登录成功的用户 TenantId 必须与当前解析到的租户一致。
            // 若不一致，说明本次 PasswordSignIn 命中了跨租户账号（例如宿主 admin 在租户上下文被解析），
            // 需立即登出并按“用户名或密码错误”处理，禁止宿主账号在租户内登录。
            // 该校验覆盖了两种非法路径：
            //  1) 宿主用户（TenantId=null）在租户上下文 CurrentTenant.Id != null 下登录；
            //  2) 租户用户（TenantId=Guid）在宿主上下文 CurrentTenant.Id == null 下登录（若上层过滤被绕过）。
            if (user.TenantId != CurrentTenant.Id)
            {
                Logger.LogWarning("Blocked cross-tenant login: User {UserName} TenantId={UserTenantId} != CurrentTenant {CurrentTenantId}", user.UserName, user.TenantId, CurrentTenant.Id);
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                Alerts.Danger(L["InvalidUserNameOrPassword"]);
                return Page();
            }

            // 追加约束：即使已通过租户匹配（例如租户下也存在 admin/1q2w3E*），
            // 也禁止用户名为 admin 的账号在租户上下文内使用，避免历史种子产生的 qidi/admin 绕过上一道预校验
            // （例如用户以邮箱 admin@abp.io 登录时，ReplaceEmailToUsername 已将输入转为 UserName，仍需按 UserName 判定）。
            if (CurrentTenant.Id.HasValue &&
                string.Equals(user.UserName, "admin", System.StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Blocked tenant admin login for reserved username. TenantId={TenantId}, UserId={UserId}", CurrentTenant.Id, user.Id);
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                Alerts.Danger(L["InvalidUserNameOrPassword"]);
                return Page();
            }

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
