using KnowledgeHub.Localization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Services;

namespace KnowledgeHub;

[IgnoreAntiforgeryToken]
public abstract class KnowledgeHubAppService : ApplicationService
{
    protected KnowledgeHubAppService()
    {
        LocalizationResource = typeof(KnowledgeHubResource);
    }
}
