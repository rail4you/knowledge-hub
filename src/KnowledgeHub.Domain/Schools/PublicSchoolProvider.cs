using System;
using System.Threading.Tasks;
using KnowledgeHub.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Schools;

/// <summary>
/// 「社会学校」解析器。
///
/// <para>
/// 平台层面<b>唯一</b>指定一个社会学校租户：所有公开注册用户统一进入该租户，
/// 课程 / 资源也统一共享到该租户。租户 Id 存在全局设置
/// <see cref="KnowledgeHubSettings.PublicSchoolTenantId"/> 中，由 host 管理员在管理端维护。
/// </para>
/// </summary>
public interface IPublicSchoolProvider
{
    /// <summary>取得社会学校租户 Id；未配置或配置非法时返回 null。</summary>
    Task<Guid?> GetPublicSchoolTenantIdAsync();

    /// <summary>取得社会学校租户；未配置或租户不存在时返回 null。</summary>
    Task<Tenant?> GetPublicSchoolTenantAsync();
}

public class PublicSchoolProvider : IPublicSchoolProvider, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly ITenantRepository _tenantRepository;

    public PublicSchoolProvider(
        ISettingProvider settingProvider,
        ITenantRepository tenantRepository)
    {
        _settingProvider = settingProvider;
        _tenantRepository = tenantRepository;
    }

    public async Task<Guid?> GetPublicSchoolTenantIdAsync()
    {
        var value = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.PublicSchoolTenantId);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var tenantId) || tenantId == Guid.Empty)
        {
            return null;
        }

        return tenantId;
    }

    public async Task<Tenant?> GetPublicSchoolTenantAsync()
    {
        var tenantId = await GetPublicSchoolTenantIdAsync();
        if (tenantId == null)
        {
            return null;
        }

        return await _tenantRepository.FindAsync(tenantId.Value);
    }
}
