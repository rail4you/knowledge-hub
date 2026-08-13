using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace KnowledgeHub.Permissions;

/// <summary>
/// 角色权限种子服务
///
/// 解决两个问题：
/// 1. `IdentityDataSeederContributor.SeedRolePermissionsAsync` 只在租户首次迁移时执行一次，
///    后续新增的权限无法自动应用到已有租户 → 通过 EnsurePermissionsForAllTenantsAsync 启动期自愈。
/// 2. 老租户里可能根本没有"标准角色"（LeagueAdmin/SchoolAdmin/Teacher/Student/EnterpriseUser），
///    导致授权阶段 `SetAsync` 静默失败 → 通过 EnsureRolesForTenantAsync 先建角色再授权。
///
/// 使用：
/// - DbMigrator 启动时由 IdentityDataSeederContributor 调用（保持原行为）
/// - 应用启动后由 RolePermissionEnsureHostedService 对所有现存租户再次种子
/// - 紧急修复时可通过 PermissionAdminController 的 API 手动触发
/// </summary>
public interface IRolePermissionSeeder
{
    /// <summary>对单个租户：先确保标准角色存在，再授予所有权限</summary>
    Task EnsureRolesAndPermissionsForTenantAsync(Guid tenantId);

    /// <summary>对所有租户（包括 host）</summary>
    Task EnsureRolesAndPermissionsForAllTenantsAsync();

    /// <summary>仅 host 上下文（admin 等全局角色）</summary>
    Task EnsureRolesAndPermissionsForHostAsync();

    /// <summary>确保标准账户存在（league-admin 等）</summary>
    Task EnsureStandardAccountsAsync();
}

public class RolePermissionSeeder : IRolePermissionSeeder, ITransientDependency
{
    /// <summary>系统维护的标准角色清单。host 上下文保留全局角色，租户上下文保留租户级角色。</summary>
    private static readonly string[] HostRoles = { "admin" };
    private static readonly string[] TenantRoles = { "LeagueAdmin", "SchoolAdmin", "Teacher", "Student", "EnterpriseUser" };

    /// <summary>
    /// LeagueAdmin 是"联盟审核员"角色：两级审核的第二级，跨租户审核院校已通过的资源。
    /// 除资源浏览/预览/联盟审核外不授予任何管理权限，避免越权做院校审核或后台管理。
    /// </summary>
    private static readonly HashSet<string> LeagueAdminPermissions = new()
    {
        KnowledgeHubPermissions.Resources.Default,
        KnowledgeHubPermissions.Resources.LeagueAudit,
        KnowledgeHubPermissions.Resources.Download,
        KnowledgeHubPermissions.Resources.ViewRecommendation,
    };

    /// <summary>
    /// 联盟独有权限：SchoolAdmin（院校管理员）不得拥有，避免院校审核员越权做联盟审核/物理删除/直播管理。
    /// </summary>
    private static readonly string[] SchoolAdminForbiddenPermissions =
    {
        KnowledgeHubPermissions.Resources.LeagueAudit,
        KnowledgeHubPermissions.Resources.PhysicalDelete,
        KnowledgeHubPermissions.RecruitmentLive.Manage,
    };

    private readonly IPermissionManager _permissionManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly IdentityUserManager _identityUserManager;
    private readonly ILogger<RolePermissionSeeder> _logger;

    public RolePermissionSeeder(
        IPermissionManager permissionManager,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager identityRoleManager,
        IdentityUserManager identityUserManager,
        ILogger<RolePermissionSeeder> logger)
    {
        _permissionManager = permissionManager;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _roleRepository = roleRepository;
        _identityRoleManager = identityRoleManager;
        _identityUserManager = identityUserManager;
        _logger = logger;
    }

    public async Task EnsureRolesAndPermissionsForAllTenantsAsync()
    {
        // 先在 host 上下文跑一遍（admin 等全局角色）
        await EnsureRolesAndPermissionsForHostAsync();

        // 再对每个租户各跑一遍
        var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
        foreach (var tenant in tenants)
        {
            await EnsureRolesAndPermissionsForTenantAsync(tenant.Id);
        }
    }

    [UnitOfWork]
    public virtual async Task EnsureRolesAndPermissionsForHostAsync()
    {
        foreach (var roleName in HostRoles)
        {
            await EnsureRoleExistsAsync(roleName);
        }
        await GrantAllRolePermissionsAsync();
    }

    [UnitOfWork]
    public virtual async Task EnsureRolesAndPermissionsForTenantAsync(Guid tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            foreach (var roleName in TenantRoles)
            {
                await EnsureRoleExistsAsync(roleName);
            }
            await GrantAllRolePermissionsAsync();
        }
    }

    /// <summary>
    /// 在当前租户上下文中确保指定角色存在。
    /// 注意：IdentityRoleManager 内部已经按当前租户过滤，所以直接调用即可。
    /// </summary>
    private async Task EnsureRoleExistsAsync(string roleName)
    {
        try
        {
            // 先查询是否已存在
            var existing = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
            if (existing != null)
            {
                return;
            }

            var role = new IdentityRole(
                Guid.NewGuid(),
                roleName,
                tenantId: _currentTenant.Id  // null 在 host 上下文表示全局
            )
            {
                IsStatic = false,
                IsPublic = true
            };

            await _identityRoleManager.CreateAsync(role);
        }
        catch (Exception)
        {
            // 并发场景下角色可能已被另一个请求创建，吞掉异常即可。
        }
    }

    /// <summary>
    /// 真正的种子逻辑。封装在这里便于：
    /// 1) 被多个调用方复用（数据种子 / 启动期自愈 / API）；
    /// 2) 集中维护"角色 → 权限"映射，避免散落多处导致漏配；
    /// 3) 通过 `GrantAsync` 幂等覆盖，无需关心历史状态。
    /// </summary>
    private async Task GrantAllRolePermissionsAsync()
    {
        // ── LeagueAdmin：联盟审核员（两级审核的第二级，只负责联盟审核） ──
        // 之前误把 LeagueAdmin 配成了"全平台管理权"，导致联盟审核员也能做院校审核（SchoolAudit）。
        // 重新处理为：仅资源浏览 + 联盟审核。旧的宽泛授权通过 SyncLeagueAdminPermissionsAsync 收回。
        await SyncLeagueAdminPermissionsAsync();

        // ── SchoolAdmin：院校管理员 ──
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Delete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Download);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.SchoolAudit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ManageCategory);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewStatistics);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Manage);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Assign);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Execute);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TeachingAgents.Review);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Delete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.Enroll);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Courses.ManageEnrollment);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ReviewJob);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ExportReport);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ManageApplication);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Employment.ViewMyApplication);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.RecruitmentLive.Create);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.News.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.News.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.News.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.News.Delete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.News.Review);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.News.Publish);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.Delete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.ManageEnrollment);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.IssueCertificate);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.MicroMajors.ViewStatistics);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.Review);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.Score);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.Export);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Practicum.ViewStatistics);

        // 租户级管理员可管理本租户的角色与用户（身份模块权限）
        await GrantAsync("SchoolAdmin", "AbpIdentity.Roles");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Roles.Create");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Roles.Update");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Roles.Delete");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Roles.ManagePermissions");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Users");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Users.Create");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Users.Update");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Users.Delete");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Users.ManagePermissions");
        await GrantAsync("SchoolAdmin", "AbpIdentity.Users.Update.ManageRoles");
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TenantInfo.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TenantInfo.Edit);

        // 收回历史遗留的"联盟独有"权限（LeagueAudit / PhysicalDelete / RecruitmentLive.Manage）
        // 院校管理员只做第一级院校审核，不能做第二级联盟审核。
        foreach (var forbidden in SchoolAdminForbiddenPermissions)
        {
            await RevokeAsync("SchoolAdmin", forbidden);
        }

        // ── Teacher：教师（修复：补齐缺失的 Courses.Delete） ──
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Create);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Edit);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Download);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantAsync("Teacher", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Manage);
        await GrantAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Assign);
        await GrantAsync("Teacher", KnowledgeHubPermissions.TeachingAgents.Review);

        // 课程权限：修复后补齐 Courses.Delete
        await GrantAsync("Teacher", KnowledgeHubPermissions.Courses.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Courses.Create);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Courses.Edit);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Courses.Delete);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Courses.Enroll);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Courses.ManageEnrollment);

        await GrantAsync("Teacher", KnowledgeHubPermissions.Employment.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Employment.ManageApplication);
        await GrantAsync("Teacher", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.RecruitmentLive.Create);

        await GrantAsync("Teacher", KnowledgeHubPermissions.News.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.News.Create);
        await GrantAsync("Teacher", KnowledgeHubPermissions.News.Edit);

        await GrantAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Create);
        await GrantAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Edit);
        await GrantAsync("Teacher", KnowledgeHubPermissions.MicroMajors.Delete);
        await GrantAsync("Teacher", KnowledgeHubPermissions.MicroMajors.ManageEnrollment);
        await GrantAsync("Teacher", KnowledgeHubPermissions.MicroMajors.IssueCertificate);

        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.Create);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.Edit);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.Review);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.Score);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.Export);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Practicum.ViewStatistics);

        // ── Student：学生（只读） ──
        await GrantAsync("Student", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.Resources.Download);
        await GrantAsync("Student", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantAsync("Student", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("Student", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("Student", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.TeachingAgents.Execute);

        await GrantAsync("Student", KnowledgeHubPermissions.Courses.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.Courses.Enroll);

        await GrantAsync("Student", KnowledgeHubPermissions.Employment.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantAsync("Student", KnowledgeHubPermissions.Employment.ViewMyApplication);

        await GrantAsync("Student", KnowledgeHubPermissions.News.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.MicroMajors.Default);

        await GrantAsync("Student", KnowledgeHubPermissions.Practicum.Default);

        // ── EnterpriseUser：企业用户 ──
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.Resources.Download);

        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.Search.Default);

        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.AI.CareerGuidance);

        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.Default);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.Employment.ScheduleInterview);

        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantAsync("EnterpriseUser", KnowledgeHubPermissions.RecruitmentLive.Create);

        // ── admin：host 管理员（两级审核都可做） ──
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.Delete);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.Download);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.SchoolAudit);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.LeagueAudit);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.ManageCategory);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.ViewStatistics);
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantAsync("admin", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantAsync("admin", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("admin", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantAsync("admin", KnowledgeHubPermissions.TeachingAgents.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.TeachingAgents.Manage);
        await GrantAsync("admin", KnowledgeHubPermissions.TeachingAgents.Assign);
        await GrantAsync("admin", KnowledgeHubPermissions.TeachingAgents.Execute);
        await GrantAsync("admin", KnowledgeHubPermissions.TeachingAgents.Review);

        await GrantAsync("admin", KnowledgeHubPermissions.Courses.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Courses.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.Courses.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.Courses.Delete);
        await GrantAsync("admin", KnowledgeHubPermissions.Courses.Enroll);
        await GrantAsync("admin", KnowledgeHubPermissions.Courses.ManageEnrollment);

        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.Review);
        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.Score);
        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.Export);
        await GrantAsync("admin", KnowledgeHubPermissions.Practicum.ViewStatistics);

        await GrantAsync("admin", KnowledgeHubPermissions.News.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.News.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.News.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.News.Delete);
        await GrantAsync("admin", KnowledgeHubPermissions.News.Review);
        await GrantAsync("admin", KnowledgeHubPermissions.News.Publish);
        await GrantAsync("admin", KnowledgeHubPermissions.News.ManageComment);

        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.Delete);
        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.ManageEnrollment);
        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.IssueCertificate);
        await GrantAsync("admin", KnowledgeHubPermissions.MicroMajors.ViewStatistics);

        await GrantAsync("admin", KnowledgeHubPermissions.Majors.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Majors.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.Majors.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.Majors.Delete);

        await GrantAsync("admin", KnowledgeHubPermissions.TenantInfo.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.TenantInfo.Edit);

        await GrantAsync("admin", KnowledgeHubPermissions.DoubleHigh.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.DoubleHigh.ManageProject);
        await GrantAsync("admin", KnowledgeHubPermissions.DoubleHigh.ManageIndicator);
        await GrantAsync("admin", KnowledgeHubPermissions.DoubleHigh.CollectData);
        await GrantAsync("admin", KnowledgeHubPermissions.DoubleHigh.ExportReport);
        await GrantAsync("admin", KnowledgeHubPermissions.DoubleHigh.ViewAll);

        await GrantAsync("admin", KnowledgeHubPermissions.Employment.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.PublishJob);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ReviewJob);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ManageResume);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ScheduleInterview);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ManageGuidance);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ManageOutcome);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ViewStatistics);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ExportReport);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ManageApplication);
        await GrantAsync("admin", KnowledgeHubPermissions.Employment.ViewMyApplication);
        await GrantAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.RecruitmentLive.Manage);
    }

    /// <summary>
    /// LeagueAdmin 采用"权威式"授权：先收回旧的宽泛权限，再授予审核所需的最小权限集。
    /// 因为历史数据里 LeagueAdmin 被授予过全平台权限（含 SchoolAudit），单纯 SetAsync(true)
    /// 是只增不减的，必须显式 SetAsync(false) 收回，才能真正做到"联盟审核员只负责联盟审核"。
    /// </summary>
    private async Task SyncLeagueAdminPermissionsAsync()
    {
        try
        {
            var all = await _permissionManager.GetAllAsync("R", "LeagueAdmin");
            foreach (var p in all)
            {
                // 只收回"已授权"且不在目标集合内的权限，避免对未授权的权限做无谓写入
                if (p.Providers != null && p.Providers.Count > 0 && !LeagueAdminPermissions.Contains(p.Name))
                {
                    try
                    {
                        await _permissionManager.SetAsync(p.Name, "R", "LeagueAdmin", false);
                    }
                    catch (Exception)
                    {
                        // 单条收回失败不阻断整体
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RolePermissionSeeder] LeagueAdmin 权限收紧失败，继续。");
        }

        foreach (var permissionName in LeagueAdminPermissions)
        {
            await GrantAsync("LeagueAdmin", permissionName);
        }
    }

    /// <summary>
    /// 确保标准账户存在：
    /// - league-admin / 123456（host 全局，LeagueAdmin 角色，跨租户联盟审核）
    /// 注意：必须通过接口代理调用（如 HostedService），内部 this.xxx 调用不会触发
    /// [UnitOfWork] 拦截器，IdentityUserManager 内部需要 DbContext 会报错。
    /// </summary>
    [UnitOfWork]
    public virtual async Task EnsureStandardAccountsAsync()
    {
        using (_currentTenant.Change(null))
        {
            await EnsureUserWithRoleAsync(
                userName: "league-admin",
                password: "123456",
                roleName: "LeagueAdmin",
                displayName: "联盟审核员");
        }
    }

    private async Task EnsureUserWithRoleAsync(string userName, string password, string roleName, string displayName)
    {
        try
        {
            var existing = await _identityUserManager.FindByNameAsync(userName);
            if (existing != null)
            {
                if (!await _identityUserManager.IsInRoleAsync(existing, roleName))
                {
                    var role = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
                    if (role != null)
                    {
                        await _identityUserManager.AddToRoleAsync(existing, roleName);
                    }
                }
                return;
            }

            var user = new IdentityUser(Guid.NewGuid(), userName, $"{userName}@default.com")
            {
                Name = displayName
            };

            var createResult = await _identityUserManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "[RolePermissionSeeder] 创建标准账户 {UserName} 失败: {Errors}",
                    userName,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            var roleExists = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
            if (roleExists != null)
            {
                var addRoleResult = await _identityUserManager.AddToRoleAsync(user, roleName);
                if (!addRoleResult.Succeeded)
                {
                    _logger.LogWarning(
                        "[RolePermissionSeeder] 为标准账户 {UserName} 分配角色 {RoleName} 失败: {Errors}",
                        userName,
                        roleName,
                        string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogWarning("[RolePermissionSeeder] 角色 {RoleName} 不存在，无法分配给 {UserName}。", roleName, userName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RolePermissionSeeder] 确保标准账户 {UserName} 失败。", userName);
        }
    }

    /// <summary>
    /// 通过 IPermissionManager 写入授权。
    /// `SetAsync(..., true)` 等价于"确保已授权"，重复调用幂等。
    /// 角色不存在时 `SetAsync` 内部会抛异常，被外层 try/catch 吞掉。
    /// </summary>
    private async Task GrantAsync(string roleName, string permissionName)
    {
        try
        {
            await _permissionManager.SetAsync(permissionName, "R", roleName, true);
        }
        catch (Exception)
        {
            // 角色在该租户不存在等情况 — 静默跳过。
            // 不向调用方抛异常，保证种子流程不被单个失败打断。
        }
    }

    /// <summary>
    /// 收回某个角色上的指定权限（用于收紧历史遗留的越权授权）。
    /// </summary>
    private async Task RevokeAsync(string roleName, string permissionName)
    {
        try
        {
            await _permissionManager.SetAsync(permissionName, "R", roleName, false);
        }
        catch (Exception)
        {
            // 角色不存在等情况 — 静默跳过。
        }
    }
}
