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

        group.AddFeature(
            KnowledgeHubFeatures.SpecialEducation,
            "false",
            L("Features:SpecialEducation"),
            L("Features:SpecialEducationDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.VoiceAssistant,
            "true",
            L("Features:VoiceAssistant"),
            L("Features:VoiceAssistantDescription")
        );

        // ── V2.0 教学资源库主线：默认关闭，按租户灰度开通 ──
        group.AddFeature(
            KnowledgeHubFeatures.LibraryReview,
            "false",
            L("Features:LibraryReview"),
            L("Features:LibraryReviewDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.BuildTask,
            "false",
            L("Features:BuildTask"),
            L("Features:BuildTaskDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.CrossSchoolRef,
            "false",
            L("Features:CrossSchoolRef"),
            L("Features:CrossSchoolRefDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.EnterpriseWorkspace,
            "false",
            L("Features:EnterpriseWorkspace"),
            L("Features:EnterpriseWorkspaceDescription")
        );

        group.AddFeature(
            KnowledgeHubFeatures.OrganizationValidity,
            "false",
            L("Features:OrganizationValidity"),
            L("Features:OrganizationValidityDescription")
        );
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<KnowledgeHubResource>(name);
    }
}