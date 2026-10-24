using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Schools.Dtos;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Configuration;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
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
/// </summary>
public class SchoolAppService : KnowledgeHubAppService, ISchoolAppService
{
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IPublicSchoolProvider _publicSchoolProvider;
    private readonly ISchoolDomainResolver _domainResolver;
    private readonly ISchoolDomainOptions _domainOptions;
    private readonly IConfiguration _configuration;
    private readonly ISettingManager _settingManager;
    private readonly ISettingProvider _settingProvider;
    private readonly IDataFilter _dataFilter;

    public SchoolAppService(
        ISchoolRepository schoolRepository,
        ITenantRepository tenantRepository,
        ITenantInfoRepository tenantInfoRepository,
        IMajorRepository majorRepository,
        IResourceLibraryRepository libraryRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IPublicSchoolProvider publicSchoolProvider,
        ISchoolDomainResolver domainResolver,
        ISchoolDomainOptions domainOptions,
        IConfiguration configuration,
        ISettingManager settingManager,
        ISettingProvider settingProvider,
        IDataFilter dataFilter)
    {
        _schoolRepository = schoolRepository;
        _tenantRepository = tenantRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _majorRepository = majorRepository;
        _libraryRepository = libraryRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _publicSchoolProvider = publicSchoolProvider;
        _domainResolver = domainResolver;
        _domainOptions = domainOptions;
        _configuration = configuration;
        _settingManager = settingManager;
        _settingProvider = settingProvider;
        _dataFilter = dataFilter;
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

        return await ToDtoAsync(school);
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

            var libraries = await AsyncExecuter.ToListAsync(
                (await _libraryRepository.GetQueryableAsync()).Where(x =>
                    x.OwnerTenantId == tenantId &&
                    x.Status == ResourceLibraryStatus.Active));

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
        var ownerTenant = await _tenantRepository.FindAsync(library.OwnerTenantId);
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

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resourceCount = await AsyncExecuter.CountAsync(
                (await _resourceRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            var courseCount = await AsyncExecuter.CountAsync(
                (await _courseRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));

            return new SchoolLibraryDto
            {
                Id = library.Id,
                Name = library.Name,
                Description = library.Description,
                CoverUrl = library.CoverUrl,
                PrimaryMajorId = library.PrimaryMajorId,
                PrimaryMajorName = majorName,
                OwnerTenantId = library.OwnerTenantId,
                OwnerTenantName = ownerTenant?.Name,
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
