using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
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
        // 租户上下文（如 qidi-admin）：只能查询本租户权限，禁止跨租户（尤其禁止查询 host 的角色权限）。
        EnsureTargetTenantAllowed(tenantId);

        using (_currentTenant.Change(tenantId, null))
        {
            var grants = await _permissionManager.GetAllAsync(providerName, providerKey);
            return grants.Select(g => new PermissionGrantInfoDto { Name = g.Name, IsGranted = g.IsGranted }).ToList();
        }
    }

    public async Task SetForTenantAsync(Guid? tenantId, string permissionName, string providerName, string providerKey, bool isGranted)
    {
        // 租户上下文：仅允许修改本租户权限。
        EnsureTargetTenantAllowed(tenantId);

        using (_currentTenant.Change(tenantId, null))
        {
            await _permissionManager.SetAsync(permissionName, providerName, providerKey, isGranted);
        }
    }

    /// <summary>
    /// 租户上下文（如 qidi-admin）下：仅允许传入与当前租户一致的 targetTenantId。
    /// host 全局管理员不受限，可传入 null（host）或任意租户。
    /// </summary>
    private void EnsureTargetTenantAllowed(Guid? targetTenantId)
    {
        if (!_currentTenant.Id.HasValue)
        {
            return;
        }

        if (!targetTenantId.HasValue || targetTenantId.Value != _currentTenant.Id.Value)
        {
            throw new UserFriendlyException("仅全局管理员可操作其他租户的权限授予。");
        }
    }
}

public class PermissionGrantInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}
