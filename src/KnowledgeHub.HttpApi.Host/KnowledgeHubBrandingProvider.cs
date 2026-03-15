using Microsoft.Extensions.Localization;
using KnowledgeHub.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace KnowledgeHub;

[Dependency(ReplaceServices = true)]
public class KnowledgeHubBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<KnowledgeHubResource> _localizer;

    public KnowledgeHubBrandingProvider(IStringLocalizer<KnowledgeHubResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
