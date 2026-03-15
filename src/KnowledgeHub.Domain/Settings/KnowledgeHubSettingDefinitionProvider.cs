using Volo.Abp.Settings;

namespace KnowledgeHub.Settings;

public class KnowledgeHubSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(KnowledgeHubSettings.MySetting1));
    }
}
