using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace KnowledgeHub.UnifiedAuth;

/// <summary>
/// 改密入口统一走账户服务：本地 AbpUsers 的哈希仅作为回退副本。
/// 覆盖 ABP Account 的 ProfileAppService，前端 /api/account/my-profile/change-password 无需改动。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService), typeof(ProfileAppService))]
public class UnifiedProfileAppService : ProfileAppService
{
    private readonly IUnifiedAuthService _unified;

    public UnifiedProfileAppService(
        IdentityUserManager userManager,
        IOptions<IdentityOptions> identityOptions,
        IUnifiedAuthService unified)
        : base(userManager, identityOptions)
    {
        _unified = unified;
    }

    public override async Task ChangePasswordAsync(ChangePasswordInput input)
    {
        if (!_unified.Enabled || CurrentTenant.Id == null || string.IsNullOrWhiteSpace(CurrentUser.UserName))
        {
            await base.ChangePasswordAsync(input);
            return;
        }

        try
        {
            await _unified.SyncPasswordAsync(
                CurrentTenant.Id.Value, CurrentUser.UserName!, input.CurrentPassword, input.NewPassword);
        }
        catch (UnifiedAuthException ex)
        {
            throw new UserFriendlyException(ex.Message);
        }

        try
        {
            // 同步本地哈希：关闭统一账户时仍可用同一密码登录。
            await base.ChangePasswordAsync(input);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "本地密码同步失败，统一账户密码已更新");
        }
    }
}
