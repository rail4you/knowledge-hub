using KnowledgeHub.Localization;
using Volo.Abp.Application.Services;

namespace KnowledgeHub;

/* Inherit your application services from this class.
 */
public abstract class KnowledgeHubAppService : ApplicationService
{
    protected KnowledgeHubAppService()
    {
        LocalizationResource = typeof(KnowledgeHubResource);
    }
}
