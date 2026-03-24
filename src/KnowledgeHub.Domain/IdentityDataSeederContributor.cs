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
    private readonly IPermissionManager _permissionManager;

    public IdentityDataSeederContributor(
        IIdentityRoleRepository identityRoleRepository,
        IdentityRoleManager identityRoleManager,
        IPermissionManager permissionManager)
    {
        _identityRoleRepository = identityRoleRepository;
        _identityRoleManager = identityRoleManager;
        _permissionManager = permissionManager;
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

        await SeedRolePermissionsAsync();
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

    private async Task SeedRolePermissionsAsync()
    {
        // League Admin - Full access
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Create, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Edit, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Delete, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Download, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.SchoolAudit, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.LeagueAudit, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.ManageCategory, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.PhysicalDelete, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.ViewStatistics, true);
        
        // Search permissions - LeagueAdmin
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.ManageIndex, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // School Admin - School level access
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Create, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Edit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Delete, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Download, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.SchoolAudit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ManageCategory, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewStatistics, true);

        // Search permissions - SchoolAdmin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ManageIndex, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // Teacher - Can create and manage own resources
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Edit, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Download, true);

        // Search permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.ManageIndex, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // Courses permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Edit, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Enroll, true);

        // Student - Read-only access
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.Download, true);

        // Search permissions - Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Search.Default, true);

        // Courses permissions - Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Enroll, true);

        // Enterprise User - Limited access
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Download, true);

        // Search permissions - EnterpriseUser
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Search.Default, true);
    }

    private async Task GrantPermissionAsync(string roleName, string permission, bool isGranted)
    {
        try
        {
            await _permissionManager.SetAsync(permission, "R", roleName, isGranted);
        }
        catch
        {
            // Permission might not exist yet, skip
        }
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
