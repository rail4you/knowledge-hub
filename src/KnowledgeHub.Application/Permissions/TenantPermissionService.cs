using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Permissions;

public interface ITenantPermissionService : ITransientDependency
{
    Task SetForTenantAsync(Guid? tenantId, string permissionName, string providerName, string providerKey, bool isGranted);
}

public class TenantPermissionService : ITenantPermissionService
{
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public TenantPermissionService(
        IPermissionManager permissionManager,
        ICurrentTenant currentTenant)
    {
        _permissionManager = permissionManager;
        _currentTenant = currentTenant;
    }

    public async Task SetForTenantAsync(Guid? tenantId, string permissionName, string providerName, string providerKey, bool isGranted)
    {
        using (_currentTenant.Change(tenantId, null))
        {
            await _permissionManager.SetAsync(permissionName, providerName, providerKey, isGranted);
        }
    }
}
