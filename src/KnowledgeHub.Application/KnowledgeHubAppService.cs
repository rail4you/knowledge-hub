using KnowledgeHub.Localization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Services;

namespace KnowledgeHub;

public abstract class KnowledgeHubAppService : ApplicationService
{
    protected KnowledgeHubAppService()
    {
        LocalizationResource = typeof(KnowledgeHubResource);
    }
}
