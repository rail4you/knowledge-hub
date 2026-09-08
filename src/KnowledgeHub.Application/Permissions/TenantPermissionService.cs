using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
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
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public TenantPermissionService(
        IPermissionManager permissionManager,
        ICurrentTenant currentTenant,
        IPermissionDefinitionManager permissionDefinitionManager)
    {
        _permissionManager = permissionManager;
        _currentTenant = currentTenant;
        _permissionDefinitionManager = permissionDefinitionManager;
    }

    public async Task<object> GetForTenantAsync(Guid? tenantId, string providerName, string providerKey)
    {
        // 租户上下文（如 qidi-admin）：只能查询本租户权限，禁止跨租户（尤其禁止查询 host 的角色权限）。
        EnsureTargetTenantAllowed(tenantId);

        using (_currentTenant.Change(tenantId, null))
        {
            var grants = (await _permissionManager.GetAllAsync(providerName, providerKey)).ToList();

            // 目标是租户角色时：过滤掉 host-only 权限（如品牌设置、账号有效期）。
            // 与 ABP PermissionChecker 的运行时语义对齐（租户侧对 host 权限鉴权恒为 false），
            // 前端弹窗直接不展示，从源头避免"看得到、点得上"的越权错觉。
            if (tenantId.HasValue)
            {
                var tenantInvisible = await GetTenantInvisiblePermissionNamesAsync();
                grants = grants.Where(g => !tenantInvisible.Contains(g.Name)).ToList();
            }

            return grants.Select(g => new PermissionGrantInfoDto { Name = g.Name, IsGranted = g.IsGranted }).ToList();
        }
    }

    public async Task SetForTenantAsync(Guid? tenantId, string permissionName, string providerName, string providerKey, bool isGranted)
    {
        // 租户上下文：仅允许修改本租户权限。
        EnsureTargetTenantAllowed(tenantId);

        // 纵深防御：禁止把 host-only 权限授予租户角色（即使调用方绕过前端直接调 API）。
        if (tenantId.HasValue && await IsTenantInvisiblePermissionAsync(permissionName))
        {
            throw new UserFriendlyException($"权限 {permissionName} 仅全局管理员可用，不可授予租户角色。");
        }

        using (_currentTenant.Change(tenantId, null))
        {
            await _permissionManager.SetAsync(permissionName, providerName, providerKey, isGranted);
        }
    }

    /// <summary>
    /// 租户角色不可见的权限名集合：即定义上不适用于租户侧的权限
    /// （MultiTenancySide 不包含 Tenant，如 Host-only 的品牌设置、账号有效期）。
    /// 与 ABP PermissionChecker 运行时语义保持一致。
    /// </summary>
    private async Task<HashSet<string>> GetTenantInvisiblePermissionNamesAsync()
    {
        var permissions = await _permissionDefinitionManager.GetPermissionsAsync();
        return permissions
            .Where(p => !p.MultiTenancySide.HasFlag(MultiTenancySides.Tenant))
            .Select(p => p.Name)
            .ToHashSet();
    }

    private async Task<bool> IsTenantInvisiblePermissionAsync(string permissionName)
    {
        var permission = await _permissionDefinitionManager.GetOrNullAsync(permissionName);
        // 未定义的权限名：保持原有行为（交由 PermissionManager 处理），这里不拦截。
        if (permission == null)
        {
            return false;
        }
        return !permission.MultiTenancySide.HasFlag(MultiTenancySides.Tenant);
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
