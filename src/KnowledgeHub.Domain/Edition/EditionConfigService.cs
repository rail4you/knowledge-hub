using System.Threading.Tasks;
using KnowledgeHub.Features;
using KnowledgeHub.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Features;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace KnowledgeHub.Edition;

public interface IEditionConfigService
{
    Task<string> GetEditionAsync();
    Task<bool> IsBasicEditionAsync();
    Task<bool> IsStandardEditionAsync();
    Task<int> GetMaxTenantCountAsync();
    Task<bool> IsAllianceEnabledAsync();
    Task<bool> IsTwoLevelApprovalEnabledAsync();
}

public class EditionConfigService : IEditionConfigService, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly IFeatureChecker _featureChecker;

    public EditionConfigService(
        ISettingProvider settingProvider,
        IFeatureChecker featureChecker)
    {
        _settingProvider = settingProvider;
        _featureChecker = featureChecker;
    }

    public async Task<string> GetEditionAsync()
    {
        return await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.InstalledEdition)
               ?? KnowledgeHubEditions.Basic;
    }

    public async Task<bool> IsBasicEditionAsync()
    {
        var edition = await GetEditionAsync();
        return edition == KnowledgeHubEditions.Basic;
    }

    public async Task<bool> IsStandardEditionAsync()
    {
        var edition = await GetEditionAsync();
        return edition == KnowledgeHubEditions.Standard;
    }

    public async Task<int> GetMaxTenantCountAsync()
    {
        var value = await _featureChecker.GetOrNullAsync(KnowledgeHubFeatures.MaxTenantCount) ?? "1";
        if (int.TryParse(value, out var count))
        {
            return count;
        }
        return 1;
    }

    public async Task<bool> IsAllianceEnabledAsync()
    {
        return await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.Alliance);
    }

    public async Task<bool> IsTwoLevelApprovalEnabledAsync()
    {
        return await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.TwoLevelApproval);
    }
}