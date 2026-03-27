using System.Threading.Tasks;
using KnowledgeHub.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace KnowledgeHub.Install;

public interface IInstallStatusService
{
    Task<bool> IsInstalledAsync();
}

public class InstallStatusService : IInstallStatusService, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;

    public InstallStatusService(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task<bool> IsInstalledAsync()
    {
        var isInstalled = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.IsInstalled);
        return isInstalled == "true";
    }
}