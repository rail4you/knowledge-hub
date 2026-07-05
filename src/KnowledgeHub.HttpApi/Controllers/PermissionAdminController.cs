using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.HttpApi.Controllers;

/// <summary>
/// 权限管理 — 维护 / 诊断端点
///
/// 这些端点只应在 host 上下文调用（因为它要跨租户操作权限）。
/// 在租户上下文中调用时，tenant 字段为空、跨租户操作会被跳过。
/// </summary>
[Area("app")]
[RemoteService(Name = "KnowledgeHub")]
[Route("api/knowledge-hub/admin/permissions")]
public class PermissionAdminController : AbpController
{
    private readonly IRolePermissionSeeder _seeder;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ILogger<PermissionAdminController> _logger;

    // 构建时常量：可作为"代码已加载"的探针。
    // 重启 API 后这个时间戳会变；未重启则保持旧值。
    private static readonly DateTime BuildStamp = new DateTime(2026, 7, 5, 14, 30, 0, DateTimeKind.Utc);

    public PermissionAdminController(
        IRolePermissionSeeder seeder,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        IIdentityUserRepository userRepository,
        IIdentityRoleRepository roleRepository,
        IdentityUserManager userManager,
        IPermissionManager permissionManager,
        ILogger<PermissionAdminController> logger)
    {
        _seeder = seeder;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userManager = userManager;
        _permissionManager = permissionManager;
        _logger = logger;
    }

    /// <summary>
    /// 手动重种子所有租户的权限（含建角色）。无需重启服务。
    /// POST /api/knowledge-hub/admin/permissions/reseed
    /// </summary>
    [HttpPost("reseed")]
    public async Task<ReseedResultDto> ReseedAllAsync([FromBody] ReseedRequestDto? input)
    {
        var result = new ReseedResultDto();
        var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
        result.Tenants = new List<TenantReseedResultDto>();

        _logger.LogInformation("[PermissionAdmin] 手动触发权限重种子，租户数={Count}", tenants.Count);

        foreach (var tenant in tenants)
        {
            try
            {
                await _seeder.EnsureRolesAndPermissionsForTenantAsync(tenant.Id);
                result.Tenants.Add(new TenantReseedResultDto
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    Success = true,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PermissionAdmin] 重种子租户 {TenantName} 失败", tenant.Name);
                result.Tenants.Add(new TenantReseedResultDto
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    Success = false,
                    Error = ex.Message,
                });
            }
        }
        result.SuccessCount = result.Tenants.Count(t => t.Success);
        result.FailureCount = result.Tenants.Count(t => !t.Success);
        return result;
    }

    /// <summary>
    /// 把指定用户加入指定角色（如果已经加入则跳过）。
    /// POST /api/knowledge-hub/admin/permissions/assign-role
    /// body: { tenantId, userName, roleName }
    /// </summary>
    [HttpPost("assign-role")]
    public async Task<AssignRoleResultDto> AssignRoleAsync([FromBody] AssignRoleRequestDto input)
    {
        var result = new AssignRoleResultDto { TenantId = input.TenantId, UserName = input.UserName, RoleName = input.RoleName };

        using (_currentTenant.Change(input.TenantId))
        {
            var user = await _userRepository.FindByNormalizedUserNameAsync(input.UserName.ToUpperInvariant());
            if (user == null)
            {
                result.Error = $"租户 {input.TenantId} 下不存在用户 {input.UserName}";
                return result;
            }
            result.UserId = user.Id;

            var role = await _roleRepository.FindByNormalizedNameAsync(input.RoleName.ToUpperInvariant());
            if (role == null)
            {
                result.Error = $"租户 {input.TenantId} 下不存在角色 {input.RoleName}";
                return result;
            }
            result.RoleId = role.Id;

            // 检查是否已加入
            var existing = await _userRepository.GetRolesAsync(user.Id);
            if (existing.Any(r => r.Id == role.Id))
            {
                result.AlreadyInRole = true;
                return result;
            }

            await _userManager.AddToRoleAsync(user, role.Name);
            result.Success = true;
        }
        return result;
    }

    /// <summary>
    /// 列出所有租户（host 上下文调用），用于在诊断时定位 tenantId。
    /// GET /api/knowledge-hub/admin/permissions/tenants
    /// </summary>
    [HttpGet("tenants")]
    public async Task<List<TenantInfoDto>> ListTenantsAsync()
    {
        var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
        return tenants.Select(t => new TenantInfoDto
        {
            Id = t.Id,
            Name = t.Name,
        }).ToList();
    }

    /// <summary>
    /// 探针端点：确认新代码是否已加载到运行中的 API 进程。
    /// GET /api/knowledge-hub/admin/permissions/ping
    /// 返回 buildStamp，如果时间戳是 2026-07-05T14:30:00Z 之前的旧值，说明代码没刷新。
    /// </summary>
    [HttpGet("ping")]
    public object Ping()
    {
        return new
        {
            buildStamp = BuildStamp,
            currentUtc = DateTime.UtcNow,
            hasSeeder = _seeder != null,
        };
    }

    /// <summary>
    /// 返回当前登录用户的真实授权（基于数据库实时查询，不走前端缓存）。
    /// GET /api/knowledge-hub/admin/permissions/whoami
    /// </summary>
    [HttpGet("whoami")]
    public async Task<WhoAmIDto> WhoAmIAsync()
    {
        var dto = new WhoAmIDto
        {
            UserId = CurrentUser.Id ?? Guid.Empty,
            UserName = CurrentUser.UserName ?? string.Empty,
            IsAuthenticated = CurrentUser.IsAuthenticated,
            TenantId = CurrentUser.TenantId,
        };

        // 当前用户的角色
        if (CurrentUser.Id.HasValue)
        {
            using (_currentTenant.Change(CurrentUser.TenantId))
            {
                var userRoles = await _userRepository.GetRolesAsync(CurrentUser.Id.Value);
                dto.RoleNames = userRoles.Select(r => r.Name).ToList();

                // 关键权限的实测状态
                dto.Permissions = new Dictionary<string, bool>();
                foreach (var role in userRoles)
                {
                    var grants = await _permissionManager.GetAllAsync("R", role.Name);
                    foreach (var g in grants.Where(x => x.IsGranted && x.Name.StartsWith("KnowledgeHub.Courses")))
                    {
                        dto.Permissions[g.Name] = true;
                    }
                }
            }
        }
        return dto;
    }

    /// <summary>
    /// 查询指定用户在指定租户下的角色与权限诊断信息。
    /// GET /api/knowledge-hub/admin/permissions/diagnose?tenantId=...&userName=wwq
    /// </summary>
    [HttpGet("diagnose")]
    public async Task<UserPermissionDiagnosticDto> DiagnoseAsync(Guid tenantId, string userName)
    {
        var dto = new UserPermissionDiagnosticDto
        {
            TenantId = tenantId,
            UserName = userName,
        };

        using (_currentTenant.Change(tenantId))
        {
            // 1) 找用户
            var user = await _userRepository.FindByNormalizedUserNameAsync(userName.ToUpperInvariant());
            if (user == null)
            {
                dto.Error = $"租户 {tenantId} 下不存在用户 {userName}";
                return dto;
            }
            dto.UserId = user.Id;

            // 2) 取该用户的角色
            var userRoles = await _userRepository.GetRolesAsync(user.Id);
            dto.RoleNames = userRoles.Select(r => r.Name).ToList();

            // 3) 对每个角色，查它的授权
            dto.RolePermissions = new List<RolePermissionDto>();
            foreach (var role in userRoles)
            {
                var grants = await _permissionManager.GetAllAsync("R", role.Name);
                dto.RolePermissions.Add(new RolePermissionDto
                {
                    RoleName = role.Name,
                    GrantedCount = grants.Count(g => g.IsGranted),
                    TotalCount = grants.Count,
                    CoursesCreate = grants.Any(g => g.IsGranted && g.Name == KnowledgeHubPermissions.Courses.Create),
                    CoursesEdit = grants.Any(g => g.IsGranted && g.Name == KnowledgeHubPermissions.Courses.Edit),
                    CoursesDelete = grants.Any(g => g.IsGranted && g.Name == KnowledgeHubPermissions.Courses.Delete),
                });
            }
        }
        return dto;
    }
}

public class TenantInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ReseedRequestDto
{
    /// <summary>为 null / 空 时表示全部租户</summary>
    public List<Guid>? TenantIds { get; set; }
}

public class AssignRoleRequestDto
{
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public class AssignRoleResultDto
{
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public bool Success { get; set; }
    public bool AlreadyInRole { get; set; }
    public string? Error { get; set; }
}

public class WhoAmIDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public Guid? TenantId { get; set; }
    public List<string> RoleNames { get; set; } = new();
    /// <summary>当前用户在 KnowledgeHub.Courses.* 下的授权情况</summary>
    public Dictionary<string, bool> Permissions { get; set; } = new();
}

public class ReseedResultDto
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<TenantReseedResultDto> Tenants { get; set; } = new();
}

public class TenantReseedResultDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class UserPermissionDiagnosticDto
{
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public List<string> RoleNames { get; set; } = new();
    public List<RolePermissionDto> RolePermissions { get; set; } = new();
    public string? Error { get; set; }
}

public class RolePermissionDto
{
    public string RoleName { get; set; } = string.Empty;
    public int GrantedCount { get; set; }
    public int TotalCount { get; set; }
    public bool CoursesCreate { get; set; }
    public bool CoursesEdit { get; set; }
    public bool CoursesDelete { get; set; }
}