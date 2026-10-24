using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.News;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Controllers;

[Route("api/public")]
public class TenantListController : AbpControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<KnowledgeHub.Courses.Course, Guid> _courseRepository;
    private readonly IRepository<KnowledgeHub.Resources.Resource, Guid> _resourceRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ISchoolDomainOptions _schoolDomainOptions;
    private readonly ITenantResolveResultAccessor _tenantResolveResultAccessor;

    public TenantListController(
        ITenantRepository tenantRepository,
        IRepository<KnowledgeHub.Courses.Course, Guid> courseRepository,
        IRepository<KnowledgeHub.Resources.Resource, Guid> resourceRepository,
        IdentityUserManager userManager,
        ISettingProvider settingProvider,
        ISchoolRepository schoolRepository,
        ISchoolDomainOptions schoolDomainOptions,
        ITenantResolveResultAccessor tenantResolveResultAccessor)
    {
        _tenantRepository = tenantRepository;
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _userManager = userManager;
        _settingProvider = settingProvider;
        _schoolRepository = schoolRepository;
        _schoolDomainOptions = schoolDomainOptions;
        _tenantResolveResultAccessor = tenantResolveResultAccessor;
    }

    // 登录页/公开下拉需要租户名列表，保持匿名
    [AllowAnonymous]
    [HttpGet("tenants")]
    public async Task<List<TenantInfoDto>> GetTenants()
    {
        // 域名绑定：在学校域名下（租户上下文）只暴露当前学校，
        // 避免把「有哪些学校、叫什么」公开给任意访客，也避免登录页下拉能选到别的学校。
        // 平台域名（host 上下文）保持返回全量，维持平台级门户与租户切换能力。
        if (CurrentTenant.Id.HasValue)
        {
            var current = await _tenantRepository.FindAsync(CurrentTenant.Id.Value);
            var currentList = new List<TenantInfoDto>();
            if (current != null)
            {
                currentList.Add(new TenantInfoDto { Id = current.Id.ToString(), Name = current.Name });
            }

            return currentList;
        }

        var tenants = await _tenantRepository.GetListAsync();

        var result = new List<TenantInfoDto>
        {
            new TenantInfoDto { Id = null, Name = "全局" }
        };

        result.AddRange(tenants
            .Select(t => new TenantInfoDto { Id = t.Id.ToString(), Name = t.Name }));

        return result;
    }

    /// <summary>
    /// 当前访问域名对应的学校信息（匿名）。
    ///
    /// <para>供前端在启动时判断「当前是不是某所学校的专属域名」：</para>
    /// <list type="bullet">
    /// <item><description><c>isSchoolDomain=true</c> → 锁定租户、隐藏机构下拉、清掉残留 <c>__tenant</c> cookie；</description></item>
    /// <item><description><c>isSchoolDomain=false</c> → 平台域名，维持平台级门户与租户切换。</description></item>
    /// </list>
    /// </summary>
    [AllowAnonymous]
    [HttpGet("current-tenant")]
    public async Task<CurrentTenantDto> GetCurrentTenant()
    {
        var resolvedByDomain = _tenantResolveResultAccessor.Result?.AppliedResolvers?
            .Contains(ISchoolDomainResolver.ResolverName) == true;

        if (!CurrentTenant.Id.HasValue || !resolvedByDomain)
        {
            return new CurrentTenantDto
            {
                IsSchoolDomain = false,
                BaseDomains = _schoolDomainOptions.GetBaseDomains().ToList()
            };
        }

        var tenant = await _tenantRepository.FindAsync(CurrentTenant.Id.Value);
        var school = await _schoolRepository.FindByTenantIdAsync(CurrentTenant.Id.Value);

        return new CurrentTenantDto
        {
            IsSchoolDomain = true,
            TenantId = CurrentTenant.Id.Value.ToString(),
            TenantName = tenant?.Name,
            SchoolName = school?.Name,
            LogoUrl = school?.LogoUrl,
            Domain = school?.Domain,
            Slug = school?.Slug,
            BaseDomains = _schoolDomainOptions.GetBaseDomains().ToList()
        };
    }

    /// <summary>
    /// 登录失败时查询当前租户下该用户的锁定状态与剩余尝试次数，供登录页提示。
    /// 匿名访问（登录页本身未登录）。用户不存在时也返回统一的默认值，
    /// 避免通过该接口探测账号是否存在。
    /// </summary>
    [AllowAnonymous]
    [HttpGet("login-attempt-info")]
    public async Task<LoginAttemptInfoDto> GetLoginAttemptInfo(string userName)
    {
        var maxFailedAttempts = await GetMaxFailedAttemptsAsync();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return new LoginAttemptInfoDto
            {
                UserExists = false,
                MaxFailedAccessAttempts = maxFailedAttempts,
                AccessFailedCount = 0
            };
        }

        var user = await _userManager.FindByNameAsync(userName.Trim())
                   ?? await _userManager.FindByEmailAsync(userName.Trim());

        if (user == null)
        {
            return new LoginAttemptInfoDto
            {
                UserExists = false,
                MaxFailedAccessAttempts = maxFailedAttempts,
                AccessFailedCount = 0
            };
        }

        var accessFailedCount = await _userManager.GetAccessFailedCountAsync(user);
        var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
        var isLockedOut = lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow;

        return new LoginAttemptInfoDto
        {
            UserExists = true,
            MaxFailedAccessAttempts = maxFailedAttempts,
            AccessFailedCount = accessFailedCount,
            RemainingAttempts = Math.Max(0, maxFailedAttempts - accessFailedCount),
            IsLockedOut = isLockedOut,
            LockoutEnd = lockoutEnd
        };
    }

    private async Task<int> GetMaxFailedAttemptsAsync()
    {
        // ABP 设置键（IdentitySettingNames.Lockout.MaxFailedAccessAttempts），默认 5。
        var value = await _settingProvider.GetOrNullAsync("Abp.Identity.Lockout.MaxFailedAccessAttempts");
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 5;
    }

    [Authorize]
    [HttpGet("tenants-with-stats")]
    public async Task<List<TenantWithStatsDto>> GetTenantsWithStats()
    {
        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<TenantWithStatsDto>();

        foreach (var tenant in tenants)
        {
            var tenantId = tenant.Id;
            var courseCount = await _courseRepository.CountAsync(x => x.TenantId == tenantId);
            var resourceCount = await _resourceRepository.CountAsync(x => x.TenantId == tenantId);

            result.Add(new TenantWithStatsDto
            {
                Id = tenant.Id.ToString(),
                Name = tenant.Name,
                CourseCount = (int)courseCount,
                ResourceCount = (int)resourceCount,
                StudentCount = 0
            });
        }

        return result.OrderByDescending(x => x.CourseCount).ToList();
    }

    [Authorize]
    [HttpGet("tenant-stats/{tenantId}")]
    public async Task<TenantStatsDto> GetTenantStats(Guid tenantId)
    {
        var courseCount = await _courseRepository.CountAsync(x => x.TenantId == tenantId);
        var resourceCount = await _resourceRepository.CountAsync(x => x.TenantId == tenantId);

        return new TenantStatsDto
        {
            CourseCount = (int)courseCount,
            ResourceCount = (int)resourceCount,
            StudentCount = 0,
            MicroMajorCount = 0,
            NewsCount = 0
        };
    }
}

public class TenantInfoDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>当前访问域名对应的学校信息（域名绑定探测结果）。</summary>
public class CurrentTenantDto
{
    /// <summary>当前域名是否命中某所学校的专属域名（true = 学校域名，false = 平台域名）。</summary>
    public bool IsSchoolDomain { get; set; }

    /// <summary>学校租户 Id（仅学校域名有值）。</summary>
    public string? TenantId { get; set; }

    /// <summary>租户名（仅学校域名有值）。</summary>
    public string? TenantName { get; set; }

    /// <summary>学校档案名（仅学校域名有值）。</summary>
    public string? SchoolName { get; set; }

    /// <summary>学校徽标（仅学校域名有值）。</summary>
    public string? LogoUrl { get; set; }

    /// <summary>学校自有域名（仅学校域名有值）。</summary>
    public string? Domain { get; set; }

    /// <summary>学校子域名前缀（仅学校域名有值）。</summary>
    public string? Slug { get; set; }

    /// <summary>平台基础域名列表。</summary>
    public List<string> BaseDomains { get; set; } = new();
}

public class TenantWithStatsDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
    public int StudentCount { get; set; }
}

public class TenantStatsDto
{
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
    public int StudentCount { get; set; }
    public int MicroMajorCount { get; set; }
    public int NewsCount { get; set; }
}

public class LoginAttemptInfoDto
{
    /// <summary>用户是否存在（当前租户下）。不存在的账号统一返回 false，避免枚举。</summary>
    public bool UserExists { get; set; }

    /// <summary>最大允许失败次数（锁定阈值）。</summary>
    public int MaxFailedAccessAttempts { get; set; }

    /// <summary>当前已失败次数。</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>剩余可失败次数（MaxFailedAccessAttempts - AccessFailedCount）。</summary>
    public int RemainingAttempts { get; set; }

    /// <summary>是否已锁定。</summary>
    public bool IsLockedOut { get; set; }

    /// <summary>锁定结束时间（未锁定时为 null）。</summary>
    public DateTimeOffset? LockoutEnd { get; set; }
}
