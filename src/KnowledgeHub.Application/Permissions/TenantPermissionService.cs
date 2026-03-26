using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Permissions;

public interface ITenantPermissionService : ITransientDependency
{
    Task<object> GetForTenantAsync(Guid? tenantId, string providerName, string providerKey);
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

    public async Task<object> GetForTenantAsync(Guid? tenantId, string providerName, string providerKey)
    {
        using (_currentTenant.Change(tenantId, null))
        {
            var grants = await _permissionManager.GetAllAsync(providerName, providerKey);
            return grants.Select(g => new PermissionGrantInfoDto { Name = g.Name, IsGranted = g.IsGranted }).ToList();
        }
    }

    public async Task SetForTenantAsync(Guid? tenantId, string permissionName, string providerName, string providerKey, bool isGranted)
    {
        using (_currentTenant.Change(tenantId, null))
        {
            await _permissionManager.SetAsync(permissionName, providerName, providerKey, isGranted);
        }
    }
}

public class PermissionGrantInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}
