using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Schools.Dtos;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Configuration;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Schools;

/// <summary>
/// 学校档案 AppService。
///
/// <para>
/// 需求「租户对应的学校是独立实体，需要有学校简介、学校徽标等配置项，在首页上显示学校徽标和名称，
/// 点击能跳转到学校简介」。
/// </para>
///
/// <para>
/// 权限说明：类上不挂 <c>[Authorize]</c>，全部方法级标注 —— ABP 动态控制器在
/// 类级 attribute + 方法级 <c>[AllowAnonymous]</c> 组合下偶发未正确复制，故按项目既有约定拆开。
/// </para>
///
/// <para>
/// <c>[IgnoreAntiforgeryToken]</c>：管理端通过 Angular <c>RestService</c>（JWT Bearer）提交保存，
/// 浏览器仍会带上历史 antiforgery cookie，导致「token 属于其它用户」被拦为 400，
/// 与 <c>TenantInfoAppService</c> / <c>SmtpConfigAppService</c> 等保持一致。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
public class SchoolAppService : KnowledgeHubAppService, ISchoolAppService
{
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    /// <summary>课程—资源库关联表（库内课程的多对多来源）。</summary>
    private readonly IRepository<ResourceLibraryCourse, Guid> _courseLibraryLinkRepository;
    /// <summary>资源—资源库关联表（库内资源的多对多来源）。</summary>
    private readonly IRepository<ResourceLibraryResource, Guid> _resourceLibraryLinkRepository;
    private readonly IPublicSchoolProvider _publicSchoolProvider;
    private readonly ISchoolDomainResolver _domainResolver;
    private readonly ISchoolDomainOptions _domainOptions;
    private readonly IConfiguration _configuration;
    private readonly ISettingManager _settingManager;
    private readonly ISettingProvider _settingProvider;
    private readonly IDataFilter _dataFilter;
    /// <summary>库内内容计数（与门户库详情同一口径）。</summary>
    private readonly IResourceLibraryContentCounter _contentCounter;

    public SchoolAppService(
        ISchoolRepository schoolRepository,
        ITenantRepository tenantRepository,
        ITenantInfoRepository tenantInfoRepository,
        IMajorRepository majorRepository,
        IResourceLibraryRepository libraryRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<ResourceLibraryCourse, Guid> courseLibraryLinkRepository,
        IRepository<ResourceLibraryResource, Guid> resourceLibraryLinkRepository,
        IPublicSchoolProvider publicSchoolProvider,
        ISchoolDomainResolver domainResolver,
        ISchoolDomainOptions domainOptions,
        IConfiguration configuration,
        ISettingManager settingManager,
        ISettingProvider settingProvider,
        IDataFilter dataFilter,
        IResourceLibraryContentCounter contentCounter)
    {
        _schoolRepository = schoolRepository;
        _tenantRepository = tenantRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _majorRepository = majorRepository;
        _libraryRepository = libraryRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _courseLibraryLinkRepository = courseLibraryLinkRepository;
        _resourceLibraryLinkRepository = resourceLibraryLinkRepository;
        _publicSchoolProvider = publicSchoolProvider;
        _domainResolver = domainResolver;
        _domainOptions = domainOptions;
        _configuration = configuration;
        _settingManager = settingManager;
        _settingProvider = settingProvider;
        _dataFilter = dataFilter;
        _contentCounter = contentCounter;
    }

    /// <summary>获取当前租户的学校档案；档案缺失时按租户名自动补建一份。</summary>
    [Authorize(KnowledgeHubPermissions.Schools.Default)]
    public virtual async Task<SchoolDto> GetCurrentAsync()
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            throw new UserFriendlyException("当前不在租户上下文中，无法获取学校档案。");
        }

        var school = await GetOrCreateAsync(tenantId.Value);
        return await ToDtoAsync(school);
    }

    /// <summary>更新当前租户的学校档案（徽标 / 简介 / 联系方式等）。</summary>
    [Authorize(KnowledgeHubPermissions.Schools.Edit)]
    public virtual async Task<SchoolDto> UpdateCurrentAsync(UpdateSchoolDto input)
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            throw new UserFriendlyException("当前不在租户上下文中，无法更新学校档案。");
        }

        var school = await GetOrCreateAsync(tenantId.Value);
        await ApplyUpdateAsync(school, input);

        return await ToDtoAsync(school);
    }

    /// <summary>
    /// 管理端：学校列表。host 全局管理员返回全部租户对应的学校（每租户一份），
    /// 租户上下文只返回本校 —— 用于「资源库管理 → 学校管理」列表栏。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Schools.Default)]
    public virtual async Task<List<SchoolDto>> GetListAsync()
    {
        var currentTenantId = CurrentTenant.Id;

        // 租户上下文：只有本校，不必（也不应）列出其它租户
        if (currentTenantId != null)
        {
            return new List<SchoolDto> { await ToDtoAsync(await GetOrCreateAsync(currentTenantId.Value)) };
        }

        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<SchoolDto>(tenants.Count);

        // 学校档案与租户 1:1，缺档案的租户按租户名自动补建（与门户展示保持一致）
        foreach (var tenant in tenants.OrderBy(x => x.Name))
        {
            result.Add(await ToDtoAsync(await GetOrCreateAsync(tenant.Id)));
        }

        return result;
    }

    /// <summary>
    /// 管理端：读取指定租户的学校档案。host 全局管理员可跨租户；租户上下文只允许本校。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Schools.Default)]
    public virtual async Task<SchoolDto> GetByTenantIdAsync(Guid tenantId)
    {
        var targetTenantId = await ResolveManageableTenantAsync(tenantId);
        return await ToDtoAsync(await GetOrCreateAsync(targetTenantId));
    }

    /// <summary>
    /// 管理端：保存指定租户的学校档案。host 全局管理员可跨租户；租户上下文只允许本校。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Schools.Edit)]
    public virtual async Task<SchoolDto> SaveByTenantIdAsync(Guid tenantId, UpdateSchoolDto input)
    {
        var targetTenantId = await ResolveManageableTenantAsync(tenantId);
        var school = await GetOrCreateAsync(targetTenantId);
        await ApplyUpdateAsync(school, input);

        return await ToDtoAsync(school);
    }

    /// <summary>
    /// 校验「管理目标租户」：host 全局管理员可指定任意租户；
    /// 租户上下文只能操作本校（越权直接拒绝）。
    /// </summary>
    private async Task<Guid> ResolveManageableTenantAsync(Guid tenantId)
    {
        var currentTenantId = CurrentTenant.Id;

        // 租户上下文：不查租户表（租户表是 host 侧数据），只比对当前租户
        if (currentTenantId != null)
        {
            if (tenantId != Guid.Empty && tenantId != currentTenantId.Value)
            {
                throw new UserFriendlyException("只能管理本校的学校档案。");
            }

            return currentTenantId.Value;
        }

        // host 上下文：可管理任意租户的学校，但目标租户必须存在
        if (tenantId == Guid.Empty || await _tenantRepository.FindAsync(tenantId) == null)
        {
            throw new UserFriendlyException("目标租户不存在。");
        }

        return tenantId;
    }

    /// <summary>把更新入参应用到学校档案（含域名缓存失效），供当前租户 / 指定租户两条路径共用。</summary>
    private async Task ApplyUpdateAsync(School school, UpdateSchoolDto input)
    {
        if (input.Name != null)
        {
            school.SetName(input.Name);
        }

        if (input.LogoUrl != null)
        {
            school.SetLogoUrl(input.LogoUrl);
        }

        if (input.BannerUrl != null)
        {
            school.SetBannerUrl(input.BannerUrl);
        }

        if (input.Introduction != null)
        {
            school.SetIntroduction(input.Introduction);
        }

        if (input.Website != null)
        {
            school.SetWebsite(input.Website);
        }

        if (input.Address != null)
        {
            school.SetAddress(input.Address);
        }

        if (input.ContactPhone != null)
        {
            school.SetContactPhone(input.ContactPhone);
        }

        if (input.ContactEmail != null)
        {
            school.SetContactEmail(input.ContactEmail);
        }

        if (input.IsPublic.HasValue)
        {
            school.SetIsPublic(input.IsPublic.Value);
        }

        if (input.SortOrder.HasValue)
        {
            school.SetSortOrder(input.SortOrder.Value);
        }

        // 域名绑定：变更域名后必须清理解析缓存，否则旧域名仍会命中本租户（最长 10 分钟）
        var oldDomain = school.Domain;
        var oldSlug = school.Slug;

        if (input.Domain != null)
        {
            await EnsureDomainAvailableAsync(input.Domain, school.Id);
            school.SetDomain(input.Domain);
        }

        if (input.Slug != null)
        {
            await EnsureSlugAvailableAsync(input.Slug, school.Id);
            school.SetSlug(input.Slug);
        }

        await _schoolRepository.UpdateAsync(school, autoSave: true);

        if (!string.IsNullOrWhiteSpace(oldDomain) && !string.Equals(oldDomain, school.Domain, StringComparison.OrdinalIgnoreCase))
        {
            await _domainResolver.InvalidateAsync(oldDomain);
        }

        if (!string.IsNullOrWhiteSpace(oldSlug) && !string.Equals(oldSlug, school.Slug, StringComparison.OrdinalIgnoreCase))
        {
            // 旧 slug 可能对应「{slug}.{baseDomain}」形式，逐个基础域名失效
            foreach (var baseDomain in _domainOptions.GetBaseDomains())
            {
                await _domainResolver.InvalidateAsync($"{oldSlug}.{baseDomain}");
            }
        }

        // 新域名/新子域名也要失效一次：这些主机名可能已被缓存为「未命中」（TTL 10 分钟），
        // 不清的话新配的域名要等缓存过期才生效。
        if (!string.IsNullOrWhiteSpace(school.Domain))
        {
            await _domainResolver.InvalidateAsync(school.Domain);
        }

        if (!string.IsNullOrWhiteSpace(school.Slug))
        {
            foreach (var baseDomain in _domainOptions.GetBaseDomains())
            {
                await _domainResolver.InvalidateAsync($"{school.Slug}.{baseDomain}");
            }
        }
    }

    /// <summary>平台配置：读取域名绑定的基础域名设置（仅 host 全局管理员）。</summary>
    [Authorize(KnowledgeHubPermissions.Schools.ManagePublicSchool)]
    public virtual async Task<DomainSettingsDto> GetDomainSettingsAsync()
    {
        return new DomainSettingsDto
        {
            BaseDomains = _domainOptions.GetBaseDomains().ToList()
        };
    }

    /// <summary>平台配置：保存域名绑定的基础域名设置（仅 host 全局管理员）。</summary>
    [Authorize(KnowledgeHubPermissions.Schools.ManagePublicSchool)]
    public virtual async Task<DomainSettingsDto> SetBaseDomainsAsync(SetBaseDomainsDto input)
    {
        var normalized = (input.BaseDomains ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => School.NormalizeDomain(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 平台自身域名不能作为基础域名，否则平台入口会被解析成某校的子域名前缀
        var platformHost = School.NormalizeDomain(_configuration["AuthServer:Authority"]);
        if (!string.IsNullOrWhiteSpace(platformHost))
        {
            var clash = normalized.FirstOrDefault(x =>
                string.Equals(x, platformHost, StringComparison.OrdinalIgnoreCase));
            if (clash != null)
            {
                throw new UserFriendlyException($"基础域名「{clash}」就是平台自身域名，请使用独立的基础域名（如 school.{clash}）。");
            }
        }

        var value = string.Join(",", normalized);
        await _settingManager.SetGlobalAsync(KnowledgeHubSettings.DomainBaseDomains, value);

        return new DomainSettingsDto { BaseDomains = normalized };
    }

    /// <summary>校验学校自有域名未被其他学校占用。</summary>
    private async Task EnsureDomainAvailableAsync(string domain, Guid currentSchoolId)
    {
        var normalized = School.NormalizeDomain(domain);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        // 平台自身域名不能被学校占用（否则平台入口会被劫持到某所学校）
        var platformHost = School.NormalizeDomain(_configuration["AuthServer:Authority"]);
        if (!string.IsNullOrWhiteSpace(platformHost) &&
            string.Equals(platformHost, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException($"域名「{normalized}」是平台自身域名，不能分配给学校。");
        }

        var exists = await _schoolRepository.FindAsync(s => s.Domain == normalized);
        if (exists != null && exists.Id != currentSchoolId)
        {
            throw new UserFriendlyException($"域名「{normalized}」已被其他学校占用。");
        }
    }

    /// <summary>校验学校子域名前缀未被其他学校占用。</summary>
    private async Task EnsureSlugAvailableAsync(string slug, Guid currentSchoolId)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var exists = await _schoolRepository.FindAsync(s => s.Slug == normalized);
        if (exists != null && exists.Id != currentSchoolId)
        {
            throw new UserFriendlyException($"子域名前缀「{normalized}」已被其他学校占用。");
        }
    }

    /// <summary>公开：学校简介聚合页数据。</summary>
    [AllowAnonymous]
    public virtual async Task<SchoolProfileDto> GetProfileAsync(Guid tenantId)
    {
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school == null)
        {
            // 尚未建立学校档案的租户：按租户名兜底展示，简介为空
            var tenant = await _tenantRepository.FindAsync(tenantId);
            if (tenant == null)
            {
                throw new UserFriendlyException("学校不存在。");
            }

            school = new School(GuidGenerator.Create(), tenantId, tenant.Name);
        }

        var profile = new SchoolProfileDto
        {
            School = await ToDtoAsync(school)
        };

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var majors = await AsyncExecuter.ToListAsync(
                (await _majorRepository.GetQueryableAsync()).Where(x => x.TenantId == tenantId));
            var resourceQuery = await _resourceRepository.GetQueryableAsync();
            var courseQuery = await _courseRepository.GetQueryableAsync();

            foreach (var major in majors.OrderBy(x => x.Name))
            {
                var majorId = major.Id;
                var courseCount = await AsyncExecuter.CountAsync(courseQuery.Where(x => x.TenantId == tenantId && x.MajorId == majorId));
                var resourceCount = await AsyncExecuter.CountAsync(resourceQuery.Where(x => x.TenantId == tenantId && x.MajorId == majorId));

                profile.Majors.Add(new SchoolMajorDto
                {
                    Id = major.Id,
                    Name = major.Name,
                    Code = major.Code,
                    Description = major.Description,
                    CourseCount = courseCount,
                    ResourceCount = resourceCount
                });
            }

            profile.CourseCount = await AsyncExecuter.CountAsync(courseQuery.Where(x => x.TenantId == tenantId));
            profile.ResourceCount = await AsyncExecuter.CountAsync(resourceQuery.Where(x => x.TenantId == tenantId));

            // 门户「共建共享资源库」：本校**主建 + 参建**的院校共建库（跨校库）。
            //
            // 口径说明：
            //   · 主建：OwnerTenantId == 本校；参建：在本库 Members 里作为 Partner（关联学校）加入。
            //     旧实现只看 OwnerTenantId，导致「别的学校主建、本校参建」的共建库在本校门户里看不到
            //     （页面模版已按 ownerTenantId 区分主建/参建，接口却没返回参建库）。
            //   · 只看 Visibility == CoBuilt：校级私有 / 校级共享库不属于「共建资源库」板块。
            //   · IsPublic == true：门户是公开页（游客可访问），IsPublic=false 的库管理员已明确
            //     选择「不在门户展示」，这里不能给显。
            //   · 只看 Status == Active：草稿 / 归档库不上门户。
            var libraries = (await _libraryRepository.GetByMemberTenantAsync(tenantId))
                .Where(x => x.Status == ResourceLibraryStatus.Active
                            && x.Visibility == ResourceLibraryVisibility.CoBuilt
                            && x.IsPublic)
                .OrderBy(x => x.OwnerTenantId == tenantId ? 0 : 1)
                .ThenBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreationTime)
                .ToList();

            foreach (var library in libraries)
            {
                profile.Libraries.Add(await ToLibraryBriefAsync(library));
            }

            profile.LibraryCount = profile.Libraries.Count;
            profile.MajorCount = profile.Majors.Count;
        }

        return profile;
    }

    /// <summary>公开：门户展示的学校列表。</summary>
    [AllowAnonymous]
    public virtual async Task<List<SchoolDto>> GetPublicListAsync()
    {
        var query = await _schoolRepository.GetQueryableAsync();
        var schools = await AsyncExecuter.ToListAsync(
            query.Where(x => x.IsPublic).OrderBy(x => x.SortOrder).ThenBy(x => x.Name));

        var publicSchoolTenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();
        var result = new List<SchoolDto>(schools.Count);
        foreach (var school in schools)
        {
            var dto = await ToDtoAsync(school);
            dto.IsPublicSchool = publicSchoolTenantId == school.TenantId;
            result.Add(dto);
        }

        return result;
    }

    /// <summary>公开：平台唯一「社会学校」信息。</summary>
    [AllowAnonymous]
    public virtual async Task<PublicSchoolDto> GetPublicSchoolAsync()
    {
        return await BuildPublicSchoolDtoAsync();
    }

    /// <summary>平台配置：设置社会学校租户（仅 host 全局管理员）。</summary>
    [Authorize(KnowledgeHubPermissions.Schools.ManagePublicSchool)]
    public virtual async Task<PublicSchoolDto> SetPublicSchoolAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择社会学校。");
        }

        var tenant = await _tenantRepository.FindAsync(tenantId);
        if (tenant == null)
        {
            throw new UserFriendlyException("目标租户不存在。");
        }

        await _settingManager.SetGlobalAsync(
            KnowledgeHubSettings.PublicSchoolTenantId,
            tenantId.ToString());

        return await BuildPublicSchoolDtoAsync();
    }

    private async Task<School> GetOrCreateAsync(Guid tenantId)
    {
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school != null)
        {
            return school;
        }

        var tenant = await _tenantRepository.FindAsync(tenantId);
        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        var displayName = tenantInfo?.Name ?? tenant?.Name ?? "未命名学校";

        school = new School(GuidGenerator.Create(), tenantId, displayName);
        if (tenantInfo?.CoverImages != null)
        {
            school.SetBannerUrl(tenantInfo.CoverImages.Split(',').FirstOrDefault()?.Trim());
        }

        return await _schoolRepository.InsertAsync(school, autoSave: true);
    }

    private async Task<SchoolDto> ToDtoAsync(School school)
    {
        var publicSchoolTenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();
        return new SchoolDto
        {
            Id = school.Id,
            TenantId = school.TenantId,
            Name = school.Name,
            LogoUrl = school.LogoUrl,
            BannerUrl = school.BannerUrl,
            Introduction = school.Introduction,
            Website = school.Website,
            Address = school.Address,
            ContactPhone = school.ContactPhone,
            ContactEmail = school.ContactEmail,
            IsPublic = school.IsPublic,
            SortOrder = school.SortOrder,
            IsPublicSchool = publicSchoolTenantId == school.TenantId,
            Domain = school.Domain,
            Slug = school.Slug
        };
    }

    private async Task<SchoolLibraryDto> ToLibraryBriefAsync(ResourceLibrary library)
    {
        // 主建单位显示名：学校档案名（中文校名）优先 → 租户扩展信息名 → 租户名（slug 兜底）。
        // 只用 tenant.Name 会显示成 "qidi"，与同页课程卡片的「启迪学院111」不一致。
        var ownerName = (await _schoolRepository.FindByTenantIdAsync(library.OwnerTenantId))?.Name;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            ownerName = (await _tenantInfoRepository.FindByTenantIdAsync(library.OwnerTenantId))?.Name;
        }
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            ownerName = (await _tenantRepository.FindAsync(library.OwnerTenantId))?.Name;
        }

        string? majorName = null;
        if (library.PrimaryMajorId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var major = await _majorRepository.FindAsync(library.PrimaryMajorId.Value);
                majorName = major?.Name;
            }
        }

        var members = await _libraryRepository.GetMembersAsync(library.Id);

        var (courseCount, resourceCount) = await _contentCounter.CountAsync(library);

        {
            return new SchoolLibraryDto
            {
                Id = library.Id,
                Name = library.Name,
                Description = library.Description,
                CoverUrl = library.CoverUrl,
                PrimaryMajorId = library.PrimaryMajorId,
                PrimaryMajorName = majorName,
                OwnerTenantId = library.OwnerTenantId,
                OwnerTenantName = ownerName,
                ResourceCount = resourceCount,
                CourseCount = courseCount,
                Members = members.Select(m => new ResourceLibraryMemberDto
                {
                    Id = m.Id,
                    MemberTenantId = m.MemberTenantId,
                    MemberTenantName = m.MemberTenantName,
                    Role = m.Role,
                    JoinedTime = m.JoinedTime
                }).ToList()
            };
        }
    }

    private async Task<PublicSchoolDto> BuildPublicSchoolDtoAsync()
    {
        var tenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();
        if (tenantId == null)
        {
            return new PublicSchoolDto { Configured = false };
        }

        var tenant = await _tenantRepository.FindAsync(tenantId.Value);
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId.Value);
        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId.Value);

        return new PublicSchoolDto
        {
            TenantId = tenantId,
            Name = school?.Name ?? tenantInfo?.Name ?? tenant?.Name,
            LogoUrl = school?.LogoUrl,
            Configured = true
        };
    }
}
