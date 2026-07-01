using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
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
        // 只在 Host 上下文运行种子上下文，避免每个租户重复创建角色
        if (context.TenantId != null)
        {
            return;
        }

        await CreateRoleIfNotExistsAsync("LeagueAdmin");
        await CreateRoleIfNotExistsAsync("SchoolAdmin");
        await CreateRoleIfNotExistsAsync("Teacher");
        await CreateRoleIfNotExistsAsync("Student");
        await CreateRoleIfNotExistsAsync("EnterpriseUser");
        await CreateRoleIfNotExistsAsync("admin");

        await SeedRolePermissionsAsync();
        await SeedAbpPermissionsAsync();
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName)
    {
        var existingRole = await _identityRoleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(Guid.NewGuid(), roleName, null)
        {
            IsStatic = false,
            IsPublic = true
        };
        await _identityRoleManager.CreateAsync(role);
    }

    private async Task SeedRolePermissionsAsync()
    {
        // League Admin - Full access
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Create);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Edit);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Delete);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.Download);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.SchoolAudit);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.LeagueAudit);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.ManageCategory);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.PhysicalDelete);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.ViewStatistics);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.Chat);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Manage);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Assign);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Execute);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Review);

        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Create);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Edit);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Delete);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Courses.Enroll);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ReviewJob);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ExportReport);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageApplication);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ViewMyApplication);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.RecruitmentLive.Create);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.RecruitmentLive.Manage);

        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Create);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Edit);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Delete);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.ManageEnrollment);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.IssueCertificate);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.ViewStatistics);

        // News permissions - LeagueAdmin
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.Default);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.Create);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.Edit);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.Delete);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.Review);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.Publish);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.News.ManageComment);

        // School Admin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Create);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Edit);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Delete);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Download);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.SchoolAudit);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ManageCategory);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewStatistics);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Chat);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Manage);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Assign);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Execute);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Review);

        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Create);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Edit);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Delete);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Enroll);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ReviewJob);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ExportReport);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageApplication);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ViewMyApplication);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.RecruitmentLive.Create);

        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Create);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Edit);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Delete);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Review);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Publish);

        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Default);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Create);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Edit);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Delete);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.ManageEnrollment);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.IssueCertificate);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.ViewStatistics);

        // Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Create);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Edit);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.Download);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.Chat);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Manage);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Assign);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Review);

        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Create);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Edit);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Enroll);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.RecruitmentLive.Create);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageApplication);

        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.News.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.News.Create);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.News.Edit);

        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Default);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Create);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Edit);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Delete);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.ManageEnrollment);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.IssueCertificate);

        // Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.Default);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.Download);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Search.Default);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantPermissionAsync("Student", KnowledgeHubPermissions.AI.Default);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.AI.Chat);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.TeachingAgents.Execute);

        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Default);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Enroll);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.Default);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.ViewMyApplication);

        await GrantPermissionAsync("Student", KnowledgeHubPermissions.News.Default);

        await GrantPermissionAsync("Student", KnowledgeHubPermissions.MicroMajors.Default);

        // Enterprise User
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Default);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Download);

        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Search.Default);

        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.Default);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.Chat);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.Default);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.RecruitmentLive.Create);

        // Admin
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.Default);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ReviewJob);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ExportReport);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageApplication);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ViewMyApplication);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Create);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Manage);
    }

    private async Task SeedAbpPermissionsAsync()
    {
        // Admin - Full access to identity and tenant management
        var adminRoles = new[] { "admin", "LeagueAdmin", "SchoolAdmin" };
        foreach (var role in adminRoles)
        {
            await GrantPermissionAsync(role, "AbpIdentity.Roles");
            await GrantPermissionAsync(role, "AbpIdentity.Roles.Create");
            await GrantPermissionAsync(role, "AbpIdentity.Roles.Update");
            await GrantPermissionAsync(role, "AbpIdentity.Roles.Delete");
            await GrantPermissionAsync(role, "AbpIdentity.Roles.ManagePermissions");
            await GrantPermissionAsync(role, "AbpIdentity.Users");
            await GrantPermissionAsync(role, "AbpIdentity.Users.Create");
            await GrantPermissionAsync(role, "AbpIdentity.Users.Update");
            await GrantPermissionAsync(role, "AbpIdentity.Users.Delete");
            await GrantPermissionAsync(role, "AbpIdentity.Users.ManagePermissions");
            await GrantPermissionAsync(role, "AbpIdentity.Users.Update.ManageRoles");
        }

        await GrantPermissionAsync("admin", "AbpTenantManagement.Tenants");
        await GrantPermissionAsync("admin", "AbpTenantManagement.Tenants.Create");
        await GrantPermissionAsync("admin", "AbpTenantManagement.Tenants.Update");
        await GrantPermissionAsync("admin", "AbpTenantManagement.Tenants.Delete");
        await GrantPermissionAsync("admin", "AbpTenantManagement.Tenants.ManageFeatures");
        await GrantPermissionAsync("admin", "AbpTenantManagement.Tenants.ManageConnectionStrings");
        await GrantPermissionAsync("admin", "FeatureManagement.ManageHostFeatures");
        await GrantPermissionAsync("admin", "SettingManagement.Emailing");

        await GrantPermissionAsync("LeagueAdmin", "AbpTenantManagement.Tenants");
        await GrantPermissionAsync("LeagueAdmin", "AbpTenantManagement.Tenants.Create");
        await GrantPermissionAsync("LeagueAdmin", "AbpTenantManagement.Tenants.Update");
        await GrantPermissionAsync("LeagueAdmin", "AbpTenantManagement.Tenants.Delete");
        await GrantPermissionAsync("LeagueAdmin", "AbpTenantManagement.Tenants.ManageFeatures");
        await GrantPermissionAsync("LeagueAdmin", "AbpTenantManagement.Tenants.ManageConnectionStrings");

        // Teacher - can view users (for student management)
        await GrantPermissionAsync("Teacher", "AbpIdentity.Users");
        await GrantPermissionAsync("Teacher", "AbpIdentity.Roles");

        // Student - no identity permissions needed
    }

    private async Task GrantPermissionAsync(string roleName, string permission)
    {
        try
        {
            await _permissionManager.SetAsync(permission, "R", roleName, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception granting permission {permission} to {roleName}: {ex.Message}");
        }
    }
}
