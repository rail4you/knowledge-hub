using KnowledgeHub.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class KnowledgeHubController : AbpControllerBase
{
    protected KnowledgeHubController()
    {
        LocalizationResource = typeof(KnowledgeHubResource);
    }
}
