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
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.ViewRecommendation, true);

        // Search permissions - LeagueAdmin
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.ManageIndex, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // AI permissions - LeagueAdmin
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.Chat, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.LessonPlan, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.CaseAnalysis, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.CareerGuidance, true);

        // Courses permissions - LeagueAdmin
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Create, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Edit, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Delete, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Enroll, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.PublishJob, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ReviewJob, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageResume, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ScheduleInterview, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageGuidance, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageOutcome, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ViewStatistics, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ExportReport, true);

        // School Admin - School level access
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Create, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Edit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Delete, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Download, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.SchoolAudit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ManageCategory, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewStatistics, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewRecommendation, true);

        // Search permissions - SchoolAdmin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ManageIndex, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // AI permissions - SchoolAdmin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Chat, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.LessonPlan, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CaseAnalysis, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CareerGuidance, true);

        // Courses permissions - SchoolAdmin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Create, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Edit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Delete, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Enroll, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.PublishJob, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ReviewJob, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageResume, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ScheduleInterview, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageGuidance, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageOutcome, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ViewStatistics, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ExportReport, true);

        // Teacher - Can create and manage own resources
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Edit, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Download, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.ViewRecommendation, true);

        // Search permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.ManageIndex, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // AI permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.Chat, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.LessonPlan, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.CaseAnalysis, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.CareerGuidance, true);

        // Courses permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Edit, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Enroll, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ScheduleInterview, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageGuidance, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageOutcome, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ViewStatistics, true);

        // Student - Read-only access
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.Download, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.ViewRecommendation, true);

        // Search permissions - Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Search.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Search.ViewStatistics, true);

        // AI permissions - Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.AI.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.AI.Chat, true);

        // Courses permissions - Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Enroll, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.ManageResume, true);

        // Enterprise User - Limited access
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Default, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Download, true);

        // Search permissions - EnterpriseUser
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Search.Default, true);

        // AI permissions - EnterpriseUser
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.Default, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.Chat, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.CaseAnalysis, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.CareerGuidance, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.PublishJob, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.ScheduleInterview, true);
    }

    private async Task GrantPermissionAsync(string roleName, string permission, bool isGranted)
    {
        try
        {
            await _permissionManager.SetAsync(permission, "R", roleName, isGranted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception granting permission {permission} to {roleName}: {ex.Message}");
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
