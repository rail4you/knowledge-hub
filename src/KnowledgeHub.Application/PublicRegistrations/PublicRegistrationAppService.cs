using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.PublicRegistrations.Dtos;
using KnowledgeHub.Schools;
using KnowledgeHub.Schools.Dtos;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.PublicRegistrations;

/// <summary>
/// 公开注册 AppService。
///
/// <para>
/// 需求「租户有公开的『社会学校』，该租户的学生可以自由注册」：
/// 平台层面唯一指定一个社会学校租户（设置项 <c>KnowledgeHub.PublicSchool.TenantId</c>），
/// 所有公开注册用户统一在该租户下创建，并自动获得该租户的 <c>Student</c> 角色。
/// </para>
///
/// <para>
/// 实现要点：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>IdentityUser.TenantId</c> 是 protected set，必须通过构造函数传入，
/// 因此注册时先 <c>CurrentTenant.Change(socialSchoolId)</c> 再用
/// <c>new IdentityUser(id, userName, email, CurrentTenant.Id)</c> 构造，否则会建成宿主用户。
/// </description></item>
/// <item><description>
/// 角色分配前先确认 <c>Student</c> 角色在该租户存在（<c>RolePermissionSeeder</c> 会为每个租户补建）。</description></item>
/// </list>
/// </summary>
public class PublicRegistrationAppService : KnowledgeHubAppService, IPublicRegistrationAppService
{
    private const string StudentRoleName = "Student";

    private readonly IPublicSchoolProvider _publicSchoolProvider;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly ITenantRepository _tenantRepository;

    public PublicRegistrationAppService(
        IPublicSchoolProvider publicSchoolProvider,
        IdentityUserManager identityUserManager,
        IIdentityRoleRepository roleRepository,
        ICurrentTenant currentTenant,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        ITenantRepository tenantRepository)
    {
        _publicSchoolProvider = publicSchoolProvider;
        _identityUserManager = identityUserManager;
        _roleRepository = roleRepository;
        _currentTenant = currentTenant;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _tenantRepository = tenantRepository;
    }

    /// <summary>公开注册（匿名）。</summary>
    [AllowAnonymous]
    public virtual async Task<PublicRegistrationResultDto> RegisterAsync(PublicRegistrationDto input)
    {
        var publicSchool = await _publicSchoolProvider.GetPublicSchoolTenantAsync();
        if (publicSchool == null)
        {
            throw new UserFriendlyException("平台尚未配置「社会学校」，暂未开放公开注册。");
        }

        var socialSchoolTenantId = publicSchool.Id;
        Guid userId;

        using (_currentTenant.Change(socialSchoolTenantId))
        {
            var existing = await _identityUserManager.FindByNameAsync(input.UserName);
            if (existing != null)
            {
                throw new UserFriendlyException("该用户名已被注册，请更换。");
            }

            if (!string.IsNullOrWhiteSpace(input.Email))
            {
                var existingByEmail = await _identityUserManager.FindByEmailAsync(input.Email);
                if (existingByEmail != null)
                {
                    throw new UserFriendlyException("该邮箱已被注册，请更换。");
                }
            }

            // TenantId 必须由构造函数传入（protected set），否则建成宿主用户
            var user = new IdentityUser(
                GuidGenerator.Create(),
                input.UserName,
                input.Email,
                _currentTenant.Id)
            {
                Name = input.Name,
                Surname = input.Surname
            };

            // PhoneNumber 的 set 访问器不可访问，必须走 SetPhoneNumber
            if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
            {
                user.SetPhoneNumber(input.PhoneNumber, false);
            }

            var createResult = await _identityUserManager.CreateAsync(user, input.Password);
            if (!createResult.Succeeded)
            {
                throw new UserFriendlyException(BuildErrorMessage(createResult));
            }

            await EnsureStudentRoleAsync();
            var addRoleResult = await _identityUserManager.AddToRoleAsync(user, StudentRoleName);
            if (!addRoleResult.Succeeded)
            {
                throw new UserFriendlyException(BuildErrorMessage(addRoleResult));
            }

            userId = user.Id;
        }

        var schoolName = await ResolveSchoolNameAsync(socialSchoolTenantId);

        return new PublicRegistrationResultDto
        {
            UserId = userId,
            UserName = input.UserName,
            SocialSchoolName = schoolName,
            SocialSchoolTenantId = socialSchoolTenantId
        };
    }

    /// <summary>公开：社会学校信息（注册页展示）。</summary>
    [AllowAnonymous]
    public virtual async Task<PublicSchoolDto> GetSocialSchoolAsync()
    {
        var tenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();
        if (tenantId == null)
        {
            return new PublicSchoolDto { Configured = false };
        }

        var tenant = await _tenantRepository.FindAsync(tenantId.Value);
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId.Value);

        return new PublicSchoolDto
        {
            TenantId = tenantId,
            Name = school?.Name ?? (await ResolveSchoolNameAsync(tenantId.Value)),
            LogoUrl = school?.LogoUrl,
            Configured = true
        };
    }

    private async Task EnsureStudentRoleAsync()
    {
        var role = await _roleRepository.FindByNormalizedNameAsync(StudentRoleName.ToUpperInvariant());
        if (role != null)
        {
            return;
        }

        // 兜底：租户内缺少 Student 角色时补建，避免注册成功却无任何权限
        await _roleRepository.InsertAsync(
            new IdentityRole(GuidGenerator.Create(), StudentRoleName, _currentTenant.Id)
            {
                IsStatic = false,
                IsPublic = true
            });
    }

    private async Task<string> ResolveSchoolNameAsync(Guid tenantId)
    {
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school != null)
        {
            return school.Name;
        }

        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (tenantInfo != null)
        {
            return tenantInfo.Name;
        }

        var tenant = await _tenantRepository.FindAsync(tenantId);
        return tenant?.Name ?? "社会学校";
    }

    private static string BuildErrorMessage(Microsoft.AspNetCore.Identity.IdentityResult result)
    {
        var messages = result.Errors
            .Select(e => e.Description)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToList();

        return messages.Count > 0
            ? string.Join("；", messages)
            : "注册失败，请稍后重试。";
    }
}
