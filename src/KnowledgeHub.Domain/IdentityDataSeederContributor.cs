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

    public IdentityDataSeederContributor(
        IIdentityRoleRepository identityRoleRepository,
        IdentityRoleManager identityRoleManager,
        IRolePermissionSeeder rolePermissionSeeder)
    {
        _identityRoleRepository = identityRoleRepository;
        _identityRoleManager = identityRoleManager;
        _rolePermissionSeeder = rolePermissionSeeder;
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
        await _rolePermissionSeeder.EnsureRolesAndPermissionsForTenantAsync(
            context.TenantId ?? Guid.Empty);
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName, string displayName, bool isGlobal)
    {
        var existingRole = await _identityRoleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(
            Guid.NewGuid(),
            roleName,
            null
        )
        {
            IsStatic = false,
            IsPublic = true
        };

        await _identityRoleManager.CreateAsync(role);
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName, string displayName)
    {
        var existingRole = await _identityRoleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(
            Guid.NewGuid(),
            roleName,
            null
        )
        {
            IsStatic = false,
            IsPublic = true
        };

        await _identityRoleManager.CreateAsync(role);
    }
}