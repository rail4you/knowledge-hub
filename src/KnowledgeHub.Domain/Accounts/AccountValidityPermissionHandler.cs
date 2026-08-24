using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace KnowledgeHub.Accounts;

/// <summary>
/// 账号到期「差异化权限熔断」执行器。
///
/// 原理：ABP 权限解析时 User 级授权（Provider "U"）优先于 Role 级（Provider "R"）。
/// 到期时对所有「非只读」权限写入 User 级禁止（false），覆盖角色授权，实现熔断；
/// 续期时写入 User 级允许（true），恢复该角色的编辑/管理权限。
/// </summary>
public interface IAccountValidityPermissionHandler
{
    /// <summary>
    /// 收回指定用户在指定角色下的「编辑/管理」权限，返回被收回的权限名列表（用于后续恢复）。
    /// 只读/使用类权限保留。
    /// </summary>
    Task<List<string>> RevokeEditPermissionsAsync(Guid userId, Guid? tenantId, string roleName);

    /// <summary>恢复（重新授予）指定权限列表（续期时调用）。</summary>
    Task RestorePermissionsAsync(Guid userId, Guid? tenantId, IReadOnlyList<string> permissionNames);
}

public class AccountValidityPermissionHandler : IAccountValidityPermissionHandler, ITransientDependency
{
    /// <summary>
    /// SchoolAdmin（租户管理员）保留的「只读/使用」权限。
    /// 到期后保留这些，收回其余所有被角色授予的权限。
    /// </summary>
    private static readonly HashSet<string> SchoolAdminKeepPermissions = new()
    {
        // 资源：浏览 / 下载 / 推荐
        KnowledgeHubPermissions.Resources.Default,
        KnowledgeHubPermissions.Resources.Download,
        KnowledgeHubPermissions.Resources.ViewRecommendation,
        // 检索：仅浏览
        KnowledgeHubPermissions.Search.Default,
        // 教学智能体：工具执行（不落库管理）
        KnowledgeHubPermissions.TeachingAgents.Default,
        KnowledgeHubPermissions.TeachingAgents.Execute,
        // 课程：浏览 / 报名
        KnowledgeHubPermissions.Courses.Default,
        KnowledgeHubPermissions.Courses.Enroll,
        // AI 工具：只读生成
        KnowledgeHubPermissions.AI.Default,
        KnowledgeHubPermissions.AI.Chat,
        KnowledgeHubPermissions.AI.LessonPlan,
        KnowledgeHubPermissions.AI.CaseAnalysis,
        KnowledgeHubPermissions.AI.CareerGuidance,
        // 就业：仅浏览
        KnowledgeHubPermissions.Employment.Default,
        KnowledgeHubPermissions.Employment.ViewMyApplication,
        // 招聘直播：仅浏览
        KnowledgeHubPermissions.RecruitmentLive.Default,
        // 资讯：仅浏览
        KnowledgeHubPermissions.News.Default,
        // 微专业：仅浏览
        KnowledgeHubPermissions.MicroMajors.Default,
        // 实训：仅浏览
        KnowledgeHubPermissions.Practicum.Default,
    };

    /// <summary>
    /// Teacher（教师）保留的「只读/使用」权限。
    /// 到期后收回资源上传/修改、课程/实训/直播/作业/微专业/资讯等编辑与管理权限。
    /// </summary>
    private static readonly HashSet<string> TeacherKeepPermissions = new()
    {
        KnowledgeHubPermissions.Resources.Default,
        KnowledgeHubPermissions.Resources.Download,
        KnowledgeHubPermissions.Resources.ViewRecommendation,
        KnowledgeHubPermissions.Search.Default,
        KnowledgeHubPermissions.Search.ViewStatistics,
        KnowledgeHubPermissions.TeachingAgents.Default,
        KnowledgeHubPermissions.TeachingAgents.Execute,
        KnowledgeHubPermissions.Courses.Default,
        KnowledgeHubPermissions.Courses.Enroll,
        KnowledgeHubPermissions.AI.Default,
        KnowledgeHubPermissions.AI.Chat,
        KnowledgeHubPermissions.AI.LessonPlan,
        KnowledgeHubPermissions.AI.CaseAnalysis,
        KnowledgeHubPermissions.AI.CareerGuidance,
        KnowledgeHubPermissions.Employment.Default,
        KnowledgeHubPermissions.Employment.ViewStatistics,
        KnowledgeHubPermissions.Employment.ViewMyApplication,
        KnowledgeHubPermissions.RecruitmentLive.Default,
        KnowledgeHubPermissions.News.Default,
        KnowledgeHubPermissions.MicroMajors.Default,
        KnowledgeHubPermissions.Practicum.Default,
        KnowledgeHubPermissions.Practicum.ViewStatistics,
    };

    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public AccountValidityPermissionHandler(
        IPermissionManager permissionManager,
        ICurrentTenant currentTenant)
    {
        _permissionManager = permissionManager;
        _currentTenant = currentTenant;
    }

    /// <summary>判断某角色是否受有效期管控（目前仅租户管理员与教师）。</summary>
    public static bool IsControlledRole(string roleName)
    {
        return roleName is "SchoolAdmin" or "Teacher";
    }

    public async Task<List<string>> RevokeEditPermissionsAsync(Guid userId, Guid? tenantId, string roleName)
    {
        var keepSet = GetKeepSet(roleName);
        var revoked = new List<string>();

        using (_currentTenant.Change(tenantId))
        {
            var grants = await _permissionManager.GetAllAsync("R", roleName);
            foreach (var p in grants)
            {
                // 只处理确实被该角色授予、且不属于「只读保留集」的权限
                if (keepSet.Contains(p.Name))
                {
                    continue;
                }

                if (!p.IsGranted || p.Providers == null || p.Providers.Count == 0)
                {
                    continue;
                }

                await _permissionManager.SetAsync(p.Name, "U", userId.ToString(), false);
                revoked.Add(p.Name);
            }
        }

        return revoked;
    }

    public async Task RestorePermissionsAsync(Guid userId, Guid? tenantId, IReadOnlyList<string> permissionNames)
    {
        if (permissionNames == null || permissionNames.Count == 0)
        {
            return;
        }

        using (_currentTenant.Change(tenantId))
        {
            foreach (var name in permissionNames)
            {
                await _permissionManager.SetAsync(name, "U", userId.ToString(), true);
            }
        }
    }

    private static HashSet<string> GetKeepSet(string roleName)
    {
        return roleName switch
        {
            "SchoolAdmin" => SchoolAdminKeepPermissions,
            "Teacher" => TeacherKeepPermissions,
            _ => throw new UserFriendlyException($"角色 {roleName} 不支持有效期管控")
        };
    }
}
