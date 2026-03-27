using System.Threading.Tasks;
using KnowledgeHub.Edition;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Events;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.MultiTenancy;

public class TenantCreationEventHandler : 
    ILocalEventHandler<EntityCreatingEventData<Tenant>>,
    ITransientDependency
{
    private readonly IEditionConfigService _editionConfig;

    public TenantCreationEventHandler(IEditionConfigService editionConfig)
    {
        _editionConfig = editionConfig;
    }

    [UnitOfWork]
    public async Task HandleEventAsync(EntityCreatingEventData<Tenant> eventData)
    {
        var entity = eventData.Entity;
        
        if (entity.Name == "Default")
        {
            return;
        }
        
        var maxCount = await _editionConfig.GetMaxTenantCountAsync();
        
        if (maxCount > 0)
        {
            throw new Volo.Abp.UserFriendlyException(
                "当前版本已达最大租户数限制，请升级到标准版以创建更多租户"
            );
        }
    }
}