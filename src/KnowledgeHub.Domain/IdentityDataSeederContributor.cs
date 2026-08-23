using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Entities;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using KnowledgeHub.Permissions;

namespace KnowledgeHub;

public class IdentityDataSeederContributor
    : IDataSeedContributor, ITransientDependency
{
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly IRolePermissionSeeder _rolePermissionSeeder;
    private readonly ICurrentTenant _currentTenant;

    public IdentityDataSeederContributor(
        IIdentityRoleRepository identityRoleRepository,
        IdentityRoleManager identityRoleManager,
        IRolePermissionSeeder rolePermissionSeeder,
        ICurrentTenant currentTenant)
    {
        _identityRoleRepository = identityRoleRepository;
        _identityRoleManager = identityRoleManager;
        _rolePermissionSeeder = rolePermissionSeeder;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        // Create global roles (not tied to any tenant)
        await CreateRoleIfNotExistsAsync("LeagueAdmin", "联盟管理员", isGlobal: true);

        // Create tenant-scoped roles (will be assigned within each tenant)
        await CreateRoleIfNotExistsAsync("SchoolAdmin", "院校管理员", isGlobal: false);
        await CreateRoleIfNotExistsAsync("Teacher", "教师", isGlobal: false);
        await CreateRoleIfNotExistsAsync("Student", "学生", isGlobal: false);

        // Enterprise users are global
        await CreateRoleIfNotExistsAsync("EnterpriseUser", "企业用户", isGlobal: true);

        // 把"角色 → 权限"的完整映射交给 RolePermissionSeeder，
        // 这里不再内联展开，避免维护多处导致漏配。
        // 注意：宿主上下文调用宿主角色流程，禁止传入 Guid.Empty 作为租户上下文
        // （历史 bug：传入 Guid.Empty 会以 Guid.Empty 为 TenantId 创建垃圾角色）。
        if (context.TenantId.HasValue)
        {
            await _rolePermissionSeeder.EnsureRolesAndPermissionsForTenantAsync(context.TenantId.Value);
        }
        else
        {
            await _rolePermissionSeeder.EnsureRolesAndPermissionsForHostAsync();
        }
    }

    /// <summary>
    /// 在当前会话作用域内确保角色存在。
    /// 修复历史 bug：
    ///  1. 「isGlobal=false」的租户级角色被错误创建为宿主级角色（TenantId=null），
    ///     且每次租户种子都会新建一条（宿主级同名角色堆积），
    ///     最终导致租户用户被分配到宿主级同名角色、运行时角色解析为空；
    ///  2. 宿主上下文不应创建租户级角色（租户角色由各租户种子或
    ///     RolePermissionSeeder 启动期自愈创建）。
    /// 角色仓储按当前租户过滤：宿主上下文只看到 TenantId=null 的角色，
    /// 租户上下文只看到本租户的角色，因此查找与创建的作用域天然一致。
    /// </summary>
    private async Task CreateRoleIfNotExistsAsync(string roleName, string displayName, bool isGlobal)
    {
        // 宿主上下文跳过租户级角色
        if (!isGlobal && _currentTenant.Id == null)
        {
            return;
        }

        var existingRole = await _identityRoleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(
            Guid.NewGuid(),
            roleName,
            tenantId: isGlobal ? null : _currentTenant.Id
        )
        {
            IsStatic = false,
            IsPublic = true
        };

        await _identityRoleManager.CreateAsync(role);
    }
}