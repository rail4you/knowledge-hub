using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace KnowledgeHub.Branding;

[IgnoreAntiforgeryToken]
public class BrandingAppService : KnowledgeHubAppService, IBrandingAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;

    public BrandingAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
    }

    [AllowAnonymous]
    public async Task<BrandingDto> GetAsync()
    {
        // ISettingProvider 按 用户→租户→全局→默认值 逐级回退，
        // 全局品牌对所有租户（含匿名首页）可见。
        var appTitle = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.BrandingAppTitle);
        var appSubtitle = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.BrandingAppSubtitle);
        var footerText = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.BrandingFooterText);
        var logoUrl = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.BrandingLogoUrl);

        return new BrandingDto
        {
            AppTitle = string.IsNullOrWhiteSpace(appTitle) ? "易课通" : appTitle.Trim(),
            AppSubtitle = string.IsNullOrWhiteSpace(appSubtitle) ? "知识资源库" : appSubtitle.Trim(),
            FooterText = string.IsNullOrWhiteSpace(footerText) ? "© 2026 易课通 · 知识资源库" : footerText.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? "" : logoUrl.Trim(),
        };
    }

    [Authorize(KnowledgeHubPermissions.Branding.Default)]
    public async Task<BrandingDto> UpdateAsync(UpdateBrandingDto input)
    {
        // 品牌是全局配置：租户上下文即使持有该权限也不允许写入。
        if (CurrentTenant.Id != null)
        {
            throw new AbpAuthorizationException("仅全局管理员可修改品牌设置。");
        }

        await _settingManager.SetGlobalAsync(
            KnowledgeHubSettings.BrandingAppTitle, input.AppTitle.Trim());
        await _settingManager.SetGlobalAsync(
            KnowledgeHubSettings.BrandingAppSubtitle, (input.AppSubtitle ?? "").Trim());
        await _settingManager.SetGlobalAsync(
            KnowledgeHubSettings.BrandingFooterText, (input.FooterText ?? "").Trim());
        await _settingManager.SetGlobalAsync(
            KnowledgeHubSettings.BrandingLogoUrl, (input.LogoUrl ?? "").Trim());

        return await GetAsync();
    }
}
