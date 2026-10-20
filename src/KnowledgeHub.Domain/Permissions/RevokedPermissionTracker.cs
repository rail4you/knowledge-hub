using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace KnowledgeHub.Permissions;

/// <summary>
/// 记录「运营显式取消过」的授权，使启动期种子自愈不会把权限重新授予。
///
/// <para>
/// 背景：<see cref="RolePermissionSeeder"/> 每次 API 启动都会对标准角色执行
/// <c>SetAsync(perm, "R", roleName, true)</c>（"确保已授权"）。这条语句是**只增不减**的：
/// 平台管理员在「角色 → 权限」里取消某项权限后，授权行被删除，
/// 但下一次 API 重启时种子会把它重新写回去——菜单又出现了，等于运营的调整无效。
/// </para>
///
/// <para>
/// ABP 的 <c>PermissionGrant</c> 只有"存在=已授权"的语义，没有"显式拒绝"的记录，
/// 因此这里用同一张表、另一个 ProviderName（<see cref="ProviderName"/>）存放"取消标记"：
/// 一行标记 = 「该租户下该角色被显式取消过这项权限」。
/// 该 ProviderName 不会被 ABP 的权限校验（只读 "R" / "U"）和权限管理界面（只读 "R" / "U"）命中，
/// 对运行时零影响。
/// </para>
/// </summary>
public interface IRevokedPermissionTracker
{
    /// <summary>该 (租户, 角色) 下该权限是否被运营显式取消过。</summary>
    Task<bool> IsRevokedAsync(string roleName, string permissionName);

    /// <summary>标记为「已显式取消」。</summary>
    Task MarkRevokedAsync(string roleName, string permissionName);

    /// <summary>清除取消标记（运营重新勾选了该权限）。</summary>
    Task ClearRevokedAsync(string roleName, string permissionName);

    /// <summary>
    /// 批量查询：给定若干角色，返回被显式取消过的权限名集合。
    /// 用于前端/中间件在"兜底注入后台权限"时跳过运营已取消的权限。
    /// </summary>
    Task<HashSet<string>> GetRevokedPermissionsAsync(IEnumerable<string> roleNames, Guid? tenantId);
}

public class RevokedPermissionTracker : IRevokedPermissionTracker, ITransientDependency
{
    /// <summary>取消标记使用的 ProviderName（与运行时/界面使用的 "R" / "U" 区分）。</summary>
    public const string ProviderName = "Revoked";

    private readonly IRepository<PermissionGrant, Guid> _permissionGrantRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public RevokedPermissionTracker(
        IRepository<PermissionGrant, Guid> permissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _permissionGrantRepository = permissionGrantRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task<bool> IsRevokedAsync(string roleName, string permissionName)
    {
        return await _permissionGrantRepository.AnyAsync(x =>
            x.Name == permissionName &&
            x.ProviderName == ProviderName &&
            x.ProviderKey == roleName);
    }

    public async Task MarkRevokedAsync(string roleName, string permissionName)
    {
        if (await IsRevokedAsync(roleName, permissionName))
        {
            return;
        }

        await _permissionGrantRepository.InsertAsync(
            new PermissionGrant(
                _guidGenerator.Create(),
                permissionName,
                ProviderName,
                roleName,
                _currentTenant.Id),
            autoSave: true);
    }

    public async Task ClearRevokedAsync(string roleName, string permissionName)
    {
        await _permissionGrantRepository.DeleteAsync(
            x => x.Name == permissionName &&
                 x.ProviderName == ProviderName &&
                 x.ProviderKey == roleName);
    }

    public async Task<HashSet<string>> GetRevokedPermissionsAsync(IEnumerable<string> roleNames, Guid? tenantId)
    {
        var names = (roleNames ?? Enumerable.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();

        if (names.Count == 0)
        {
            return new HashSet<string>();
        }

        using (_currentTenant.Change(tenantId))
        {
            var rows = await _permissionGrantRepository.GetListAsync(x =>
                x.ProviderName == ProviderName && names.Contains(x.ProviderKey));

            return new HashSet<string>(rows.Select(r => r.Name));
        }
    }
}
