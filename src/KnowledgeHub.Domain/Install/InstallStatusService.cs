using System.Threading.Tasks;
using KnowledgeHub.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;

namespace KnowledgeHub.Install;

public class InstallStatusService : IInstallStatusService, ITransientDependency
{
    private readonly ISettingStore _settingStore;

    public InstallStatusService(ISettingStore settingStore)
    {
        _settingStore = settingStore;
    }

    public async Task<bool> IsInstalledAsync()
    {
        var isInstalled = await _settingStore.GetOrNullAsync(
            KnowledgeHubSettings.IsInstalled,
            "H",
            null);
        return isInstalled == "true";
    }
}

public interface IInstallStatusService
{
    Task<bool> IsInstalledAsync();
}