using System;
using System.Threading.Tasks;
using KnowledgeHub.Features;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Users;

namespace KnowledgeHub.Tenants;

public interface ITenantCreationValidator
{
    Task ValidateAsync();
}

public class TenantCreationValidator : ITenantCreationValidator, ITransientDependency
{
    private readonly IFeatureFlagService _featureFlags;
    private readonly ITenantRepository _tenantRepository;

    public TenantCreationValidator(
        IFeatureFlagService featureFlags,
        ITenantRepository tenantRepository)
    {
        _featureFlags = featureFlags;
        _tenantRepository = tenantRepository;
    }

    public async Task ValidateAsync()
    {
        var maxCount = await _featureFlags.GetMaxTenantCountAsync();
        
        if (maxCount <= 0)
        {
            return;
        }

        var currentCount = await _tenantRepository.GetCountAsync();
        
        if (currentCount >= maxCount)
        {
            throw new Volo.Abp.UserFriendlyException(
                "当前版本已达最大租户数限制，请升级到标准版以创建更多租户"
            );
        }
    }
}