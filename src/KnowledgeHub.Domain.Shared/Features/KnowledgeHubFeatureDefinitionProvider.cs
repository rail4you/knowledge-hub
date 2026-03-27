using Volo.Abp.Features;
using Volo.Abp.Localization;
using KnowledgeHub.Localization;

namespace KnowledgeHub.Features;

public class KnowledgeHubFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup(
            KnowledgeHubFeatures.GroupName,
            L("Features:KnowledgeHub")
        );

        group.AddFeature(
            KnowledgeHubFeatures.Edition,
            KnowledgeHubEditions.Basic,
            L("Features:Edition"),
            L("Features:EditionDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.MaxTenantCount,
            "1",
            L("Features:MaxTenantCount"),
            L("Features:MaxTenantCountDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.Alliance,
            "false",
            L("Features:Alliance"),
            L("Features:AllianceDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.TwoLevelApproval,
            "false",
            L("Features:TwoLevelApproval"),
            L("Features:TwoLevelApprovalDescription")
        );
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<KnowledgeHubResource>(name);
    }
}