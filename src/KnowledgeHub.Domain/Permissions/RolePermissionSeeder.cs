using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Features;
using Microsoft.Extensions.Logging;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
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

    /// <summary>
    /// 只允许在**宿主（host）作用域**被授权的角色。
    ///
    /// <para>
    /// SchoolAdmin / Teacher / Student / EnterpriseUser 都是租户级角色，host 上并不存在；
    /// 早前的实现把它们也写进了 host 作用域，产生了一批"幽灵授权行"。
    /// 这些行不只是脏数据：ABP 的角色授权缓存键不含租户维度，运行时按**角色名**解析，
    /// 同一份幽灵行会被任意租户命中，从而把别人的授权结果带进当前租户
    /// （详见 <see cref="TenantAwarePermissionStore"/>）。
    /// </para>
    /// </summary>
    private static readonly HashSet<string> HostScopeRoles = new() { "admin", "LeagueAdmin" };
    private static readonly string[] TenantRoles = { "LeagueAdmin", "SchoolAdmin", "Teacher", "Student", "EnterpriseUser" };

    /// <summary>
    /// LeagueAdmin 是"联盟审核员"角色：两级审核的第二级，跨租户审核院校已通过的资源。
    /// 除资源浏览/预览/联盟审核外，仅额外持有联盟管理（全局能力），避免越权做院校审核或后台管理。
    /// </summary>
    private static readonly HashSet<string> LeagueAdminPermissions = new()
    {
        KnowledgeHubPermissions.Resources.Default,
        KnowledgeHubPermissions.Resources.LeagueAudit,
        KnowledgeHubPermissions.Resources.Download,
        KnowledgeHubPermissions.Resources.ViewRecommendation,
        KnowledgeHubPermissions.Alliance.Default,
        KnowledgeHubPermissions.Alliance.Create,
        KnowledgeHubPermissions.Alliance.Update,
        KnowledgeHubPermissions.Alliance.Delete,
        KnowledgeHubPermissions.Alliance.ManageMembers,
    };

    /// <summary>
    /// 联盟独有权限：SchoolAdmin（院校管理员）不得拥有，避免院校审核员越权做联盟审核/联盟管理/直播管理。
    /// 注意：PhysicalDelete 已从此列表移除 —— 租户管理员需审批本租户内用户（包括老师）的资源删除申请，
    /// 因此 SchoolAdmin 必须持有 PhysicalDelete 权限；审批时后端会按租户隔离，只能处理本租户资源。
    /// </summary>
    private static readonly string[] SchoolAdminForbiddenPermissions =
    {
        KnowledgeHubPermissions.Resources.LeagueAudit,
        KnowledgeHubPermissions.Alliance.Default,
        KnowledgeHubPermissions.Alliance.Create,
        KnowledgeHubPermissions.Alliance.Update,
        KnowledgeHubPermissions.Alliance.Delete,
        KnowledgeHubPermissions.Alliance.ManageMembers,
        KnowledgeHubPermissions.RecruitmentLive.Manage,
    };

    /// <summary>
    /// 租户管理（新建租户）权限：仅 host「admin」全局管理员持有。
    /// 前端 ABP 租户管理菜单（Administration → Tenant Management）的显隐依赖该权限。
    /// 租户上下文（SchoolAdmin/Teacher/Student/EnterpriseUser/租户级 admin）绝不授予。
    /// </summary>
    private static readonly string[] TenantManagementPermissions =
    {
        "AbpTenantManagement.Tenants",
        "AbpTenantManagement.Tenants.Create",
        "AbpTenantManagement.Tenants.Update",
        "AbpTenantManagement.Tenants.Delete",
        "AbpTenantManagement.Tenants.ManageFeatures",
        "AbpTenantManagement.Tenants.ManageConnectionStrings",
    };

    /// <summary>
    /// 身份管理（角色/用户）权限：仅 host「admin」与租户「SchoolAdmin」持有。
    /// 教师/学生/企业用户绝不持有，避免其看到角色管理菜单或通过接口管理角色权限
    /// （前端菜单依赖 application-configuration 的 grantedPolicies，历史脏授权会直接暴露菜单）。
    /// </summary>
    private static readonly string[] IdentityManagementPermissions =
    {
        "AbpIdentity.Roles",
        "AbpIdentity.Roles.Create",
        "AbpIdentity.Roles.Update",
        "AbpIdentity.Roles.Delete",
        "AbpIdentity.Roles.ManagePermissions",
        "AbpIdentity.Users",
        "AbpIdentity.Users.Create",
        "AbpIdentity.Users.Update",
        "AbpIdentity.Users.Delete",
        "AbpIdentity.Users.ManagePermissions",
        "AbpIdentity.Users.Update.ManageRoles",
    };

    private readonly IPermissionManager _permissionManager;
    private readonly IRepository<PermissionGrant, Guid> _permissionGrantRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<RolePermissionSeeder> _logger;
    private readonly IFeatureChecker _featureChecker;
    private readonly IDistributedCache<PermissionGrantCacheItem> _permissionCache;

    /// <summary>
    /// 本次进程内已读取的"显式取消"标记。
    ///
    /// <para>
    /// 键必须包含租户维度：角色名在多租户下不唯一（每个租户都有 SchoolAdmin），
    /// 只按角色名缓存会让 A 租户的取消标记泄漏到 B 租户，
    /// 导致 B 租户的权限被"误判为已取消"而不再补授。
    /// </para>
    /// </summary>
    private readonly Dictionary<(Guid? TenantId, string RoleName), HashSet<string>> _revokedPermissionsByRole = new();

    public RolePermissionSeeder(
        IPermissionManager permissionManager,
        IRepository<PermissionGrant, Guid> permissionGrantRepository,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IIdentityRoleRepository roleRepository,
        IIdentityUserRepository userRepository,
        IdentityRoleManager identityRoleManager,
        IdentityUserManager identityUserManager,
        IDataFilter dataFilter,
        ILogger<RolePermissionSeeder> logger,
        IFeatureChecker featureChecker,
        IDistributedCache<PermissionGrantCacheItem> permissionCache)
    {
        _permissionManager = permissionManager;
        _permissionGrantRepository = permissionGrantRepository;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _identityRoleManager = identityRoleManager;
        _identityUserManager = identityUserManager;
        _dataFilter = dataFilter;
        _logger = logger;
        _featureChecker = featureChecker;
        _permissionCache = permissionCache;
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

        // 清理历史遗留的"幽灵授权行"：host 作用域下并不存在 SchoolAdmin/Teacher/Student/EnterpriseUser
        // 这些租户级角色，早前的实现却把它们的基线权限写进了 host 作用域。
        // 这些行不只是脏数据——ABP 的角色授权缓存按角色名共享、不含租户维度，
        // 同一份幽灵行会被任意租户命中，把别的租户的授权结果带进当前租户。
        await DeleteGhostHostScopeGrantsAsync();
        await GrantAllRolePermissionsAsync();
    }

    /// <summary>
    /// 删除 host 作用域下属于**租户级角色**的授权行（幂等）。
    ///
    /// <para>
    /// 只处理 SchoolAdmin / Teacher / Student / EnterpriseUser 这几个租户级角色名；
    /// host 上真实存在的 admin / LeagueAdmin 必须保留。
    /// </para>
    /// </summary>
    private async Task DeleteGhostHostScopeGrantsAsync()
    {
        try
        {
            var ghostRoles = new[] { "SchoolAdmin", "Teacher", "Student", "EnterpriseUser" };

            // 逐条删除（不用仓储的批量删除）：批量删除走的是 ABP 的 ExecuteDelete 路径，
            // 会在当前 UnitOfWork 之外提前结束 DbContext 的使用，导致后续查询报
            // "Cannot access a disposed context instance"。
            var rows = await _permissionGrantRepository.GetListAsync(x =>
                x.TenantId == null &&
                x.ProviderName == "R" &&
                ghostRoles.Contains(x.ProviderKey));

            foreach (var row in rows)
            {
                await _permissionGrantRepository.DeleteAsync(row, autoSave: true);
            }

            if (rows.Count > 0)
            {
                _logger.LogWarning(
                    "[RolePermissionSeeder] 已清理 host 作用域的幽灵授权行 {Count} 条（租户级角色名）。",
                    rows.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RolePermissionSeeder] 清理 host 作用域幽灵授权行失败。");
        }
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

            // 自愈：租户用户被误分配到宿主级同名标准角色时，改指到本租户角色。
            // （历史 bug 产物：宿主级 Student/Teacher/SchoolAdmin 等角色堆积，
            //   租户用户被分配到宿主级角色后在运行时解析不到角色，
            //   前端把学生当成教师/把教师当成普通用户，行为不一致。）
            await RepairHostScopeRoleAssignmentsAsync(tenantId);
        }
    }

    /// <summary>
    /// 找出本租户下被分配到「宿主级标准角色」的用户，并把关联改指到其租户级同名角色。
    /// 原因：宿主级同名角色堆积（历史种子 bug）导致某些写路径把宿主角色分配给了租户用户，
    /// ABP 多租户过滤下这类关联在运行时不可见（角色解析为空）。
    ///
    /// 幂等：已修复的关联（租户级角色）不会被再次命中。
    /// </summary>
    private async Task RepairHostScopeRoleAssignmentsAsync(Guid tenantId)
    {
        List<IdentityUser> users;
        Dictionary<Guid, IdentityRole> hostRoles;
        Dictionary<string, IdentityRole> tenantRoles;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var allRoles = await _roleRepository.GetListAsync(includeDetails: false);
            hostRoles = allRoles
                .Where(r => r.TenantId == null && TenantRoles.Contains(r.Name))
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToDictionary(r => r.Id);
            tenantRoles = allRoles
                .Where(r => r.TenantId == tenantId && TenantRoles.Contains(r.Name))
                .GroupBy(r => r.Name)
                .Select(g => g.First())
                .ToDictionary(r => r.Name);

            if (hostRoles.Count == 0)
            {
                return;
            }

            users = await _userRepository.GetListAsync(includeDetails: true);
        }

        var changed = false;
        foreach (var user in users.Where(u => u.TenantId == tenantId))
        {
            var links = user.Roles?.ToList() ?? new List<IdentityUserRole>();
            foreach (var link in links)
            {
                if (!hostRoles.TryGetValue(link.RoleId, out var hostRole))
                {
                    continue;
                }

                if (!tenantRoles.TryGetValue(hostRole.Name, out var tenantRole))
                {
                    continue;
                }

                var alreadyLinked = links.Any(l => l.RoleId == tenantRole.Id);
                user.RemoveRole(hostRole.Id);
                if (!alreadyLinked)
                {
                    user.AddRole(tenantRole.Id);
                }

                changed = true;
                _logger.LogWarning(
                    "[RolePermissionSeeder] 自愈：租户用户 {UserName}({TenantId}) 的宿主级角色 {RoleName} 已改指到租户级角色 {TenantRoleId}",
                    user.UserName,
                    user.TenantId,
                    hostRole.Name,
                    tenantRole.Id);
            }
        }

        if (changed)
        {
            await _userRepository.UpdateManyAsync(users.Where(u => u.TenantId == tenantId));
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
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.RequestDelete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.PhysicalDelete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewStatistics);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ViewRecommendation);
        // 院校管理员可共享本租户资源给其他租户，并可取消共享。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.Share);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Resources.ManageShare);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.ExerciseGenerate);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.ImageGeneration);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.VideoGeneration);
        // 院校管理员可查看/管理本租户所有人的 AI 生成任务（任务监控面板）
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.AI.ManageTasks);
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

        // 学校档案：院校管理员维护本校徽标 / 简介 / 联系方式。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Schools.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Schools.Edit);

        // 资源库（多校共建）：院校管理员建库、绑定主建专业、管理合作学校成员。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.ResourceLibraries.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.ResourceLibraries.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.ResourceLibraries.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.ResourceLibraries.Delete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.ResourceLibraries.ManageMembers);

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

        // ── 特教扩展模块（整体式权限，随 Feature 开关生效） ──
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.SpecialEducation.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.SpecialEducation.TeachingDesign);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.SpecialEducation.IEP);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.SpecialEducation.Resource);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.SpecialEducation.Review);

        // 租户级管理员可管理本租户的角色与用户（身份模块权限）
        foreach (var permission in IdentityManagementPermissions)
        {
            await GrantAsync("SchoolAdmin", permission);
        }

        // 资源库管理（租户展示信息）：host「admin」管理所有租户，SchoolAdmin 管理自己所在租户。
        // 后端 GetListAsync / SaveByTenantIdAsync 已按 CurrentTenant.Id 隔离，租户管理员只能看到/修改本租户。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TenantInfo.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.TenantInfo.Edit);

        // SMTP 邮件设置：租户 SchoolAdmin 配置本租户发信服务器（写租户级 Setting）。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Smtp.Default);

        // 阿里云 OSS 设置：租户 SchoolAdmin 配置本租户 Bucket / AK（写租户级 Setting），
        // 未配置时回退 host 写入的全局配置。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Oss.Default);

        // 用户管理：院校管理员管理本租户用户（列表/创建/编辑/删除/批量导入）。
        // 后端 TenantUserAppService / UserImportAppService 均已按 CurrentTenant.Id 隔离，
        // 租户管理员只能操作本租户用户；联盟管理员（LeagueAdmin）角色仍被显式拒绝。
        // 注意：此前种子从未授予 SchoolAdmin 该组权限，导致租户管理员打不开用户导入（401）。
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Users.Default);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Users.Create);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Users.Edit);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Users.Delete);
        await GrantAsync("SchoolAdmin", KnowledgeHubPermissions.Users.Import);

        // 收回历史遗留的"联盟独有"权限（LeagueAudit / Alliance.* / RecruitmentLive.Manage）
        // 院校管理员只做第一级院校审核，不能做第二级联盟审核，也不能做联盟管理（全局能力）。
        foreach (var forbidden in SchoolAdminForbiddenPermissions)
        {
            await RevokeAsync("SchoolAdmin", forbidden);
            await RevokeByDeleteAsync("SchoolAdmin", forbidden);
        }

        // 租户角色（Teacher / Student / EnterpriseUser）同样绝不持有联盟终审与联盟管理：
        // 直接删除授权行，清理历史脏授权。LeagueAdmin 由 SyncLeagueAdminPermissionsAsync
        // 权威式收紧（allowlist 之内，含联盟管理），无需在此处理。
        foreach (var roleName in new[] { "Teacher", "Student", "EnterpriseUser" })
        {
            await RevokeByDeleteAsync(roleName, KnowledgeHubPermissions.Resources.LeagueAudit);
            await RevokeByDeleteAsync(roleName, KnowledgeHubPermissions.Alliance.Default);
            await RevokeByDeleteAsync(roleName, KnowledgeHubPermissions.Alliance.Create);
            await RevokeByDeleteAsync(roleName, KnowledgeHubPermissions.Alliance.Update);
            await RevokeByDeleteAsync(roleName, KnowledgeHubPermissions.Alliance.Delete);
            await RevokeByDeleteAsync(roleName, KnowledgeHubPermissions.Alliance.ManageMembers);
        }

        // 身份管理（角色/用户）权限仅 SchoolAdmin 持有：教师/学生/企业用户直接删除历史脏授权，
        // 保证其登录后既看不到角色管理菜单，也无法调用角色/权限相关接口。
        // LeagueAdmin 由 SyncLeagueAdminPermissionsAsync 权威式收紧（allowlist 不含身份权限）。
        foreach (var roleName in new[] { "Teacher", "Student", "EnterpriseUser" })
        {
            foreach (var permission in IdentityManagementPermissions)
            {
                await RevokeByDeleteAsync(roleName, permission);
            }
        }

        // ── Teacher：教师（修复：补齐缺失的 Courses.Delete） ──
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Create);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Edit);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.Download);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.ViewRecommendation);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.RequestDelete);
        // 教师可管理分类、查看资源统计：明确纳入 Teacher 基线（之前只靠历史脏授权，新租户会缺失）
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.ManageCategory);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Resources.ViewStatistics);
        // 教师仅可申请删除，不能审批物理删除
        await RevokeAsync("Teacher", KnowledgeHubPermissions.Resources.PhysicalDelete);
        // 教师不能直接删除资源（删除走“申请删除 → 审批”流程）：收回历史遗留的 Delete 授权
        await RevokeByDeleteAsync("Teacher", KnowledgeHubPermissions.Resources.Delete);
        // 教师不得持有两级审核权限：历史数据里 Teacher 角色被授予过
        // SchoolAudit / LeagueAudit，导致教师端出现待审核 Tab 与审核按钮。
        // 直接删除授权行（幂等），API 每次启动的自愈流程会自动清理所有租户。
        await RevokeByDeleteAsync("Teacher", KnowledgeHubPermissions.Resources.SchoolAudit);
        await RevokeByDeleteAsync("Teacher", KnowledgeHubPermissions.Resources.LeagueAudit);

        await GrantAsync("Teacher", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Search.ManageIndex);
        await GrantAsync("Teacher", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.LessonPlan);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.CaseAnalysis);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.CareerGuidance);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.ExerciseGenerate);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.ImageGeneration);
        await GrantAsync("Teacher", KnowledgeHubPermissions.AI.VideoGeneration);
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

        // 资源库（多校共建）：教师 / 学生可<b>浏览</b>本校参与共建的库（只读），
        // 建库与成员管理仍限院校管理员。
        await GrantAsync("Teacher", KnowledgeHubPermissions.ResourceLibraries.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.ResourceLibraries.Default);

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

        // ── 特教：教师可生成/管理教案、IEP、资源（审核由 SchoolAdmin 做） ──
        await GrantAsync("Teacher", KnowledgeHubPermissions.SpecialEducation.Default);
        await GrantAsync("Teacher", KnowledgeHubPermissions.SpecialEducation.TeachingDesign);
        await GrantAsync("Teacher", KnowledgeHubPermissions.SpecialEducation.IEP);
        await GrantAsync("Teacher", KnowledgeHubPermissions.SpecialEducation.Resource);
        // 指派审核制：被指派的教师可审核自己名下的待审方案（后端按 ReviewerUserId 放行），
        // 同时授予 Review 权限以便教师之间可互审；学生绝不授予。
        await GrantAsync("Teacher", KnowledgeHubPermissions.SpecialEducation.Review);

        // 资源库管理页（/admin/tenant-info）：后端已按租户隔离，SchoolAdmin 可管理本租户。
        // Teacher / Student / EnterpriseUser 不授予，显式收回历史遗留授权。
        // LeagueAdmin 由 SyncLeagueAdminPermissionsAsync 权威式收紧，此处覆盖剩余租户角色。
        await RevokeAsync("Teacher", KnowledgeHubPermissions.TenantInfo.Default);
        await RevokeAsync("Teacher", KnowledgeHubPermissions.TenantInfo.Edit);
        await RevokeAsync("Student", KnowledgeHubPermissions.TenantInfo.Default);
        await RevokeAsync("Student", KnowledgeHubPermissions.TenantInfo.Edit);
        await RevokeAsync("EnterpriseUser", KnowledgeHubPermissions.TenantInfo.Default);
        await RevokeAsync("EnterpriseUser", KnowledgeHubPermissions.TenantInfo.Edit);

        // ── Student：学生（只读） ──
        await GrantAsync("Student", KnowledgeHubPermissions.Resources.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.Resources.Download);
        await GrantAsync("Student", KnowledgeHubPermissions.Resources.ViewRecommendation);

        await GrantAsync("Student", KnowledgeHubPermissions.Search.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.Search.ViewStatistics);

        await GrantAsync("Student", KnowledgeHubPermissions.AI.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.AI.Chat);
        await GrantAsync("Student", KnowledgeHubPermissions.AI.CareerGuidance);
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

        // ── 特教：学生仅查看适配资源 + 自己的 IEP（后端按 CurrentUser.Id 过滤） ──
        await GrantAsync("Student", KnowledgeHubPermissions.SpecialEducation.Default);
        await GrantAsync("Student", KnowledgeHubPermissions.SpecialEducation.Resource);

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
        await GrantAsync("admin", KnowledgeHubPermissions.Resources.RequestDelete);
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
        await GrantAsync("admin", KnowledgeHubPermissions.AI.ExerciseGenerate);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.ImageGeneration);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.VideoGeneration);
        await GrantAsync("admin", KnowledgeHubPermissions.AI.ManageTasks);
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

        // 学校档案：host admin 可配置平台唯一「社会学校」；院校管理员可维护本校档案。
        await GrantAsync("admin", KnowledgeHubPermissions.Schools.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.Schools.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.Schools.ManagePublicSchool);

        // 资源库（多校共建）：host admin 与院校管理员都可建库 / 管成员。
        await GrantAsync("admin", KnowledgeHubPermissions.ResourceLibraries.Default);
        await GrantAsync("admin", KnowledgeHubPermissions.ResourceLibraries.Create);
        await GrantAsync("admin", KnowledgeHubPermissions.ResourceLibraries.Edit);
        await GrantAsync("admin", KnowledgeHubPermissions.ResourceLibraries.Delete);
        await GrantAsync("admin", KnowledgeHubPermissions.ResourceLibraries.ManageMembers);

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

        // 联盟管理：仅 host「admin」全局管理员可用（全局能力）。
        // host 上下文授予；租户上下文（含租户级 admin）显式收回，防止租户管理员越权管理联盟。
        if (_currentTenant.Id == null)
        {
            await GrantAsync("admin", KnowledgeHubPermissions.Alliance.Default);
            await GrantAsync("admin", KnowledgeHubPermissions.Alliance.Create);
            await GrantAsync("admin", KnowledgeHubPermissions.Alliance.Update);
            await GrantAsync("admin", KnowledgeHubPermissions.Alliance.Delete);
            await GrantAsync("admin", KnowledgeHubPermissions.Alliance.ManageMembers);
        }
        else
        {
            await RevokeByDeleteAsync("admin", KnowledgeHubPermissions.Alliance.Default);
            await RevokeByDeleteAsync("admin", KnowledgeHubPermissions.Alliance.Create);
            await RevokeByDeleteAsync("admin", KnowledgeHubPermissions.Alliance.Update);
            await RevokeByDeleteAsync("admin", KnowledgeHubPermissions.Alliance.Delete);
            await RevokeByDeleteAsync("admin", KnowledgeHubPermissions.Alliance.ManageMembers);
        }

        // 租户信息管理：仅 host「admin」全局管理员可用（GetListAsync / SaveByTenantIdAsync 均要求宿主上下文）。
        // 租户上下文绝不授予，且显式收回历史遗留授权，防止租户级 admin 看到/修改其它租户的信息。
        if (_currentTenant.Id == null)
        {
            await GrantAsync("admin", KnowledgeHubPermissions.TenantInfo.Default);
            await GrantAsync("admin", KnowledgeHubPermissions.TenantInfo.Edit);
        }
        else
        {
            await RevokeAsync("admin", KnowledgeHubPermissions.TenantInfo.Default);
            await RevokeAsync("admin", KnowledgeHubPermissions.TenantInfo.Edit);
        }

        // 租户管理（新建租户）：仅 host「admin」全局管理员可用。
        // host 上下文授予；租户上下文（含租户级 admin）显式收回，保证教师端/租户管理员
        // 看不到租户管理菜单（Administration → Tenant Management）。
        if (_currentTenant.Id == null)
        {
            foreach (var permission in TenantManagementPermissions)
            {
                await GrantAsync("admin", permission);
            }
        }
        else
        {
            foreach (var permission in TenantManagementPermissions)
            {
                await RevokeByDeleteAsync("admin", permission);
            }
        }

        // 租户角色（SchoolAdmin/Teacher/Student/EnterpriseUser）绝不持有租户管理权限：
        // 直接删除授权行。LeagueAdmin 由 SyncLeagueAdminPermissionsAsync
        // 权威式收紧（allowlist 之外全部收回），无需在此处理。
        foreach (var roleName in new[] { "SchoolAdmin", "Teacher", "Student", "EnterpriseUser" })
        {
            foreach (var permission in TenantManagementPermissions)
            {
                await RevokeByDeleteAsync(roleName, permission);
            }
        }

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

        // 用户管理：host「admin」全局管理员（UserAppService / 用户导入等依赖）。
        // 注意：KnowledgeHub.Users 不能作为"多校协同-有效期配置"的入口权限 ——
        // 它是租户用户管理权限（TenantUserAppService 依赖），租户级 SchoolAdmin 也持有，
        // 若复用会导致租户管理员看到"多校协同"菜单。入口控制改用独立权限 AccountValidity。
        await GrantAsync("admin", KnowledgeHubPermissions.Users.Default);

        // SMTP 邮件设置：host「admin」写全局回退配置（租户未配置时对所有租户生效）。
        await GrantAsync("admin", KnowledgeHubPermissions.Smtp.Default);

        // 阿里云 OSS 设置：host「admin」写全局回退配置（Bucket / AK / Endpoint）。
        await GrantAsync("admin", KnowledgeHubPermissions.Oss.Default);

        // 账号有效期（多校协同）：仅 host「admin」全局管理员，且仅在宿主上下文写入，
        // 保证多校协同菜单与账号有效期配置仅全局管理员可见/可用。
        // 租户上下文绝不授予；GrantAllPoliciesMiddleware 的注入列表也不包含该权限，
        // 因此租户级 SchoolAdmin/admin 即使拿到 KnowledgeHub.Users 也看不到该菜单。
        if (_currentTenant.Id == null)
        {
            await GrantAsync("admin", KnowledgeHubPermissions.AccountValidity.Default);

            // 特教总开关管理：仅 host 全局管理员（按租户开/关 Feature + 跨租户查看）。
            await GrantAsync("admin", KnowledgeHubPermissions.SpecialEducation.Default);
            await GrantAsync("admin", KnowledgeHubPermissions.SpecialEducation.TeachingDesign);
            await GrantAsync("admin", KnowledgeHubPermissions.SpecialEducation.IEP);
            await GrantAsync("admin", KnowledgeHubPermissions.SpecialEducation.Resource);
            await GrantAsync("admin", KnowledgeHubPermissions.SpecialEducation.Review);
            await GrantAsync("admin", KnowledgeHubPermissions.SpecialEducation.Manage);

            // 语音助手总开关管理：仅 host 全局管理员（按租户开/关 Feature）。
            await GrantAsync("admin", KnowledgeHubPermissions.VoiceAssistant.Default);
            await GrantAsync("admin", KnowledgeHubPermissions.VoiceAssistant.Manage);

            // 站点品牌设置：仅 host 全局管理员（三端统一标题/副标题/页脚/Logo）。
            await GrantAsync("admin", KnowledgeHubPermissions.Branding.Default);

            // 存储容量监控：仅 host「admin」全局管理员（跳租户统计所有租户文件容量与配额）。
            await GrantAsync("admin", KnowledgeHubPermissions.StorageMonitor.Default);

            // 身份管理（角色/用户）权限：仅 host「admin」持有 —— 与 GrantAllPoliciesMiddleware
            // 的 identityPerms 注入保持一致。之前只有中间件注入（只影响 application-configuration
            // 的 grantedPolicies，即"菜单看得见"），数据库里并没有授权行，导致
            // TenantPermissionController 上的 [Authorize("AbpIdentity.Roles.ManagePermissions")]
            // 真实鉴权失败 —— 平台管理员在「角色 → 权限」里保存时报 403，点不动。
            // 这里补齐数据库授权，让"菜单可见"与"接口可调"同源。
            foreach (var permission in IdentityManagementPermissions)
            {
                await GrantAsync("admin", permission);
            }
        }
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
    /// 授予权限（幂等）。
    ///
    /// <para>三条约束：</para>
    /// <list type="number">
    /// <item>宿主作用域只授权 host 级角色：SchoolAdmin / Teacher / Student / EnterpriseUser 都是
    /// **租户级**角色，在 host 作用域写入只会产生"幽灵授权行"；而这些行会被角色权限缓存
    /// 跨租户命中（见 <see cref="TenantAwarePermissionStore"/>），把某个租户的授权结果
    /// 污染到其它租户。</item>
    /// <item>运营在「角色 → 权限」里显式取消过的权限不再补授，保证运营的调整不会因为
    /// API 重启而失效（见 <see cref="IRevokedPermissionTracker"/>）。</item>
    /// <item>特教模块是插件式开关：租户未开通特教 Feature 时不授予，并收回已授权的部分。</item>
    /// </list>
    /// </summary>
    private async Task GrantAsync(string roleName, string permissionName)
    {
        // ① 宿主作用域：只允许 host 级角色。
        if (_currentTenant.Id == null && !HostScopeRoles.Contains(roleName))
        {
            return;
        }

        // ② 特教模块：按租户 Feature 开关门控。
        //    - 租户已开通：特教权限**不参与**周期种子（开通动作已发放，运营还可能逐项取消过，
        //      周期种子若再补授就会把取消掉的权限复活），因此直接跳过。
        //    - 租户未开通：收回可能存在的历史授权（与"禁止权限"同样的强制语义）。
        if (_currentTenant.Id.HasValue && IsSpecialEducationPermission(permissionName))
        {
            if (!await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.SpecialEducation))
            {
                await RevokeByDeleteAsync(roleName, permissionName);
            }

            return;
        }

        // ③ 运营显式取消过的权限：**不但不补授，还要把已存在的授权行删掉**。
        //    只"跳过授予"是不够的：授权行可能已经被别的路径复活
        //    （旧版本种子的缺陷、ABP 自带 PermissionDataSeedContributor、手工改库…），
        //    那时"跳过"只会让脏行永久留下，菜单照样显示。
        //    "运营取消"必须表达为「该授权行不存在」，自愈才能真正收敛。
        if ((await GetRevokedPermissionsAsync(roleName)).Contains(permissionName))
        {
            await RevokeByDeleteAsync(roleName, permissionName);
            return;
        }

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

    /// <summary>是否为特教扩展模块的权限（含组权限本身）。</summary>
    private static bool IsSpecialEducationPermission(string permissionName)
        => permissionName != null &&
           permissionName.StartsWith(KnowledgeHubPermissions.SpecialEducation.Default, StringComparison.Ordinal);

    /// <summary>
    /// 读取某角色被运营显式取消过的权限（进程内按角色缓存）。
    ///
    /// <para>
    /// 这里刻意用**种子器自己的**仓储读取，而不是注入的 IRevokedPermissionTracker：
    /// 种子器是被 HostedService（单例）从根容器解析出来的，它拿到的仓储绑定的是
    /// 根作用域的 DbContext，在别的作用域结束后就可能已释放
    /// （"Cannot access a disposed context instance"）。
    /// 种子器自己的仓储与授权写入用的是同一个 DbContext，生命周期一致。
    /// </para>
    /// </summary>
    private async Task<HashSet<string>> GetRevokedPermissionsAsync(string roleName)
    {
        // 缓存键含租户：角色名跨租户不唯一，只按角色名缓存会造成跨租户串味。
        var cacheKey = (_currentTenant.Id, roleName);
        if (_revokedPermissionsByRole.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var rows = await _permissionGrantRepository.GetListAsync(x =>
            x.ProviderName == RevokedPermissionTracker.ProviderName &&
            x.ProviderKey == roleName);

        var revoked = new HashSet<string>(rows.Select(r => r.Name));
        _revokedPermissionsByRole[cacheKey] = revoked;

        _logger.LogInformation(
            "[RolePermissionSeeder] 读取取消标记：租户={TenantId} 角色={RoleName} 命中={Count} 项。",
            _currentTenant.Id?.ToString() ?? "host", roleName, revoked.Count);

        return revoked;
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

    /// <summary>
    /// 直接删除授权行（幂等，无匹配行时不报错）。
    /// <see cref="IPermissionManager"/>.SetAsync(..., false) 对部分历史脏数据静默无操作，
    /// 收回租户管理这类"必须消失"的权限时用仓储删除，确保真正生效。
    /// 注意：调用方须处于目标租户上下文（PermissionGrant 按租户隔离）。
    /// </summary>
    private async Task RevokeByDeleteAsync(string roleName, string permissionName)
    {
        try
        {
            // 逐条删除：仓储的批量删除走 ExecuteDelete 路径，会提前结束 DbContext 的使用，
            // 导致同一轮自愈里后续的权限查询报 "disposed context"。
            var rows = await _permissionGrantRepository.GetListAsync(x =>
                x.Name == permissionName &&
                x.ProviderName == "R" &&
                x.ProviderKey == roleName);

            foreach (var row in rows)
            {
                await _permissionGrantRepository.DeleteAsync(row, autoSave: true);
            }
        }
        catch (Exception)
        {
            // 角色不存在等情况 — 静默跳过。
        }

        // 仓储删除不一定触发实体变更事件，这里显式清理权限缓存，
        // 否则被收回的权限在缓存过期前仍然生效（菜单会"删不掉"）。
        await ClearPermissionCacheAsync(roleName, permissionName);
    }

    /// <summary>清理「租户感知」的权限缓存键（与运行时使用的键格式保持一致）。</summary>
    private async Task ClearPermissionCacheAsync(string roleName, string permissionName)
    {
        try
        {
            var cacheKey = TenantAwarePermissionCacheKey.Build(
                permissionName,
                "R",
                roleName,
                _currentTenant.Id);

            await _permissionCache.RemoveAsync(cacheKey, considerUow: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RolePermissionSeeder] 清理权限缓存失败：{RoleName}/{PermissionName}", roleName, permissionName);
        }
    }
}
