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

    private readonly IDataFilter _dataFilter;

    public IdentityDataSeederContributor(
        IIdentityRoleRepository identityRoleRepository,
        IdentityRoleManager identityRoleManager,
        IPermissionManager permissionManager,
        IDataFilter dataFilter)
    {
        _identityRoleRepository = identityRoleRepository;
        _identityRoleManager = identityRoleManager;
        _permissionManager = permissionManager;
        _dataFilter = dataFilter;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        // Create global roles (not tied to any tenant)
        await CreateRoleIfNotExistsAsync("LeagueAdmin", "联盟管理员", isGlobal: true, context);
        
        // Create tenant-scoped roles (will be assigned within each tenant)
        await CreateRoleIfNotExistsAsync("SchoolAdmin", "院校管理员", isGlobal: false, context);
        await CreateRoleIfNotExistsAsync("Teacher", "教师", isGlobal: false, context);
        await CreateRoleIfNotExistsAsync("Student", "学生", isGlobal: false, context);
        
        // Enterprise users are global
        await CreateRoleIfNotExistsAsync("EnterpriseUser", "企业用户", isGlobal: true, context);

        await SeedRolePermissionsAsync();
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName, string displayName, bool isGlobal, DataSeedContext context)
    {
        IdentityRole? existingRole;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            existingRole = await _identityRoleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        }
        if (existingRole != null)
        {
            return;
        }

        var role = new IdentityRole(
            Guid.NewGuid(),
            roleName,
            isGlobal ? null : context?.TenantId
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
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Manage, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Assign, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Execute, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.TeachingAgents.Review, true);

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
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ManageApplication, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.Employment.ViewMyApplication, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.RecruitmentLive.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.RecruitmentLive.Create, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.RecruitmentLive.Manage, true);

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
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Manage, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Assign, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Execute, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Review, true);

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
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageApplication, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ViewMyApplication, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.RecruitmentLive.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.RecruitmentLive.Create, true);

        // News permissions - SchoolAdmin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Create, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Edit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Delete, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Review, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.News.Publish, true);

        // MicroMajor permissions - SchoolAdmin
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Default, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Create, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Edit, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Delete, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.ManageEnrollment, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.IssueCertificate, true);
        await GrantPermissionAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.ViewStatistics, true);

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
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Manage, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Assign, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Review, true);

        // Courses permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Edit, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Courses.Enroll, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ScheduleInterview, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.RecruitmentLive.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.RecruitmentLive.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageGuidance, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageOutcome, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ViewStatistics, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.Employment.ManageApplication, true);

        // News permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.News.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.News.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.News.Edit, true);

        // MicroMajor permissions - Teacher
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Default, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Create, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Edit, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Delete, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.ManageEnrollment, true);
        await GrantPermissionAsync("Teacher", KnowledgeHubPermissions.MicroMajors.IssueCertificate, true);

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
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.TeachingAgents.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.TeachingAgents.Execute, true);

        // Courses permissions - Student
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Courses.Enroll, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.ManageResume, true);
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.Employment.ViewMyApplication, true);

        // News permissions - Student (允许查看资讯、点赞、评论)
        // 注意：News.Create/Edit/Delete/Review/Publish/ManageComment 都是子权限，
        //       未授予 → 学生不会获得发文/审核/管理评论的权限。
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.News.Default, true);

        // MicroMajor permissions - Student (允许浏览微专业、报名)
        // 注意：MicroMajors.Create/Edit/Delete/ManageEnrollment/IssueCertificate/ViewStatistics
        //       都是子权限，未授予 → 学生不会获得管理类操作权限。
        await GrantPermissionAsync("Student", KnowledgeHubPermissions.MicroMajors.Default, true);

        // MicroMajor permissions - LeagueAdmin (full access)
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Default, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Create, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Edit, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.Delete, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.ManageEnrollment, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.IssueCertificate, true);
        await GrantPermissionAsync("LeagueAdmin", KnowledgeHubPermissions.MicroMajors.ViewStatistics, true);

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
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.RecruitmentLive.Default, true);
        await GrantPermissionAsync("EnterpriseUser", KnowledgeHubPermissions.RecruitmentLive.Create, true);

        // Admin (host) - full access to Employment and RecruitmentLive
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.Default, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.PublishJob, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ReviewJob, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageResume, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ScheduleInterview, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageGuidance, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageOutcome, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ViewStatistics, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ExportReport, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ManageApplication, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.Employment.ViewMyApplication, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Default, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Create, true);
        await GrantPermissionAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Manage, true);
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
