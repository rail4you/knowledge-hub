using Volo.Abp.Settings;

namespace KnowledgeHub.Settings;

public class KnowledgeHubSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(KnowledgeHubSettings.IsInstalled, "false", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.InstalledEdition, "Basic", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.LicenseKey, "", isVisibleToClients: false),

            // 站点品牌（全局唯一，host 超管在“品牌设置”页维护；公开可见，前端三端共用）
            new SettingDefinition(KnowledgeHubSettings.BrandingAppTitle, "易课通", isVisibleToClients: true),
            new SettingDefinition(KnowledgeHubSettings.BrandingAppSubtitle, "知识资源库", isVisibleToClients: true),
            new SettingDefinition(KnowledgeHubSettings.BrandingFooterText, "© 2026 易课通 · 知识资源库", isVisibleToClients: true),
            new SettingDefinition(KnowledgeHubSettings.BrandingLogoUrl, "", isVisibleToClients: true)
        );
    }
}
