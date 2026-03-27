using Volo.Abp.Settings;

namespace KnowledgeHub.Settings;

public class KnowledgeHubSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(KnowledgeHubSettings.IsInstalled, "false", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.InstalledEdition, "Basic", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.LicenseKey, "", isVisibleToClients: false)
        );
    }
}
