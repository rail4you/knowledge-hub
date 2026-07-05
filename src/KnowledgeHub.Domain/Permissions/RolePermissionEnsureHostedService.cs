using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using KnowledgeHub.MultiTenancy;

namespace KnowledgeHub.Permissions;

/// <summary>
/// 启动期权限自愈 HostedService
///
/// 问题背景：
/// <c>IdentityDataSeederContributor</c> 只在租户**首次迁移**时执行一次。
/// 后续新增的权限（如本次补齐的 Teacher.Courses.Delete）无法自动应用到已有租户，
/// 导致老租户下的教师账号在数据库授权缺失，出现 403。
///
/// 解决：
/// API 进程每次启动时，遍历所有租户调用 <see cref="IRolePermissionSeeder.EnsurePermissionsForTenantAsync"/>，
/// 该方法对授权写入是幂等的（已授权的会保持，已过期的会自动补齐）。
///
/// 失败容忍：单个租户失败不阻塞其它租户，确保大部分租户被自愈。
/// </summary>
public class RolePermissionEnsureHostedService : IHostedService, ITransientDependency
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IRolePermissionSeeder _seeder;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<RolePermissionEnsureHostedService> _logger;

    public RolePermissionEnsureHostedService(
        IHostApplicationLifetime lifetime,
        IRolePermissionSeeder seeder,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        ILogger<RolePermissionEnsureHostedService> logger)
    {
        _lifetime = lifetime;
        _seeder = seeder;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 多租户关闭时不处理
        if (!MultiTenancyConsts.IsEnabled)
        {
            return;
        }

        // 等应用其它启动逻辑先完成，避免和 DbMigrator 并发改权限表
        try
        {
            // 在 host 上下文重新跑一遍：补齐 admin 等全局角色的授权
            await SafeGrantAllAsync();
            await SafeGrantTenantsAsync();
        }
        catch (Exception ex)
        {
            // 任何未预期异常都不能让 API 进程启动失败
            _logger.LogWarning(ex, "[RolePermissionEnsure] 启动期权限自愈失败，应用继续启动。");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SafeGrantAllAsync()
    {
        try
        {
            // 在 host 上下文跑一次：admin 等全局角色在此处被自愈
            await _seeder.EnsureRolesAndPermissionsForAllTenantsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RolePermissionEnsure] host 全局权限自愈失败。");
        }
    }

    private async Task SafeGrantTenantsAsync()
    {
        try
        {
            var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
            _logger.LogInformation(
                "[RolePermissionEnsure] 检测到 {Count} 个租户，启动期将逐一自愈（建角色 + 授权）。",
                tenants.Count);

            var okCount = 0;
            var failCount = 0;
            foreach (var tenant in tenants)
            {
                try
                {
                    await _seeder.EnsureRolesAndPermissionsForTenantAsync(tenant.Id);
                    okCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogWarning(ex,
                        "[RolePermissionEnsure] 租户 {TenantName}({TenantId}) 自愈失败。",
                        tenant.Name, tenant.Id);
                }
            }

            _logger.LogInformation(
                "[RolePermissionEnsure] 自愈完成：成功 {Ok} 个 / 失败 {Fail} 个。",
                okCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RolePermissionEnsure] 遍历租户列表失败。");
        }
    }
}