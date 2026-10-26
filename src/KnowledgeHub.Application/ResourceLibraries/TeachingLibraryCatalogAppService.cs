using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Majors;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 公开门户「教学资源库」目录实现。
///
/// <para>分区口径：</para>
/// <list type="bullet">
/// <item><description>
/// <b>校内资源库</b>（Scope=school）：当前租户主建的库，含私有 / 公开 / 院校共建三种可见性。
/// 未登录（无租户上下文）时为空。
/// </description></item>
/// <item><description>
/// <b>公共共享资源库</b>（Scope=public）：<c>IsPublic=true</c> 且已发布、且主建学校**不是**本校的库
/// （其它学校对外公开 / 共建的库）。
/// </description></item>
/// </list>
/// </summary>
[AllowAnonymous]
public class TeachingLibraryCatalogAppService : KnowledgeHubAppService, ITeachingLibraryCatalogAppService
{
    private const string ScopeSchool = "school";
    private const string ScopePublic = "public";

    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IResourceLibraryContentCounter _contentCounter;
    private readonly IResourceLibraryAccessService _accessService;
    private readonly IMajorRepository _majorRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataFilter _dataFilter;

    public TeachingLibraryCatalogAppService(
        IResourceLibraryRepository libraryRepository,
        IResourceLibraryContentCounter contentCounter,
        IResourceLibraryAccessService accessService,
        IMajorRepository majorRepository,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        ITenantRepository tenantRepository,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _contentCounter = contentCounter;
        _accessService = accessService;
        _majorRepository = majorRepository;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<TeachingLibraryCardDto>> GetListAsync(TeachingLibraryCatalogRequestDto input)
    {
        input ??= new TeachingLibraryCatalogRequestDto();

        // scope: school 校内 / public 公共共享 / all（缺省）全部范围
        var scope = string.Equals(input.Scope, ScopeSchool, StringComparison.OrdinalIgnoreCase)
            ? ScopeSchool
            : string.Equals(input.Scope, ScopePublic, StringComparison.OrdinalIgnoreCase)
                ? ScopePublic
                : "all";

        var tenantId = CurrentTenant.Id;
        var skip = input.SkipCount < 0 ? 0 : input.SkipCount;
        var take = input.MaxResultCount <= 0 ? 12 : input.MaxResultCount;

        List<ResourceLibrary> page;
        int total;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _libraryRepository.GetQueryableAsync();
            query = query.Where(x => x.Status == ResourceLibraryStatus.Active);

            if (scope == ScopeSchool)
            {
                if (!tenantId.HasValue)
                {
                    // 游客没有本校概念：校内分区为空
                    return new PagedResultDto<TeachingLibraryCardDto>(0, new List<TeachingLibraryCardDto>());
                }

                query = query.Where(x => x.OwnerTenantId == tenantId.Value);
            }
            else if (scope == ScopePublic)
            {
                // 公共共享：对外公开的库，且不是本校主建（本校的库在「校内」范围）
                query = query.Where(x => x.IsPublic);
                if (tenantId.HasValue)
                {
                    query = query.Where(x => x.OwnerTenantId != tenantId.Value);
                }
            }
            else
            {
                // 全部范围：本校主建的库 ∪ 对外公开的库
                query = tenantId.HasValue
                    ? query.Where(x => x.OwnerTenantId == tenantId.Value || x.IsPublic)
                    : query.Where(x => x.IsPublic);
            }

            if (input.PrimaryMajorId.HasValue && input.PrimaryMajorId.Value != Guid.Empty)
            {
                var majorId = input.PrimaryMajorId.Value;
                query = query.Where(x => x.PrimaryMajorId == majorId);
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var f = input.Filter.Trim();
                query = query.Where(x => x.Name.Contains(f) || (x.Description != null && x.Description.Contains(f)));
            }

            total = await AsyncExecuter.CountAsync(query);
            page = await AsyncExecuter.ToListAsync(
                query.OrderBy(x => x.SortOrder)
                     .ThenByDescending(x => x.CreationTime)
                     .Skip(skip)
                     .Take(take));
        }

        var items = new List<TeachingLibraryCardDto>(page.Count);
        foreach (var library in page)
        {
            items.Add(await ToCardAsync(library, tenantId.HasValue && library.OwnerTenantId == tenantId.Value ? 0 : 1));
        }

        return new PagedResultDto<TeachingLibraryCardDto>(total, items);
    }

    private async Task<TeachingLibraryCardDto> ToCardAsync(ResourceLibrary library, int scope)
    {
        var (courseCount, resourceCount) = await _contentCounter.CountAsync(library);

        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var partnerNames = new List<string>();
        foreach (var m in members.Where(x => x.Role == ResourceLibraryMemberRole.Partner))
        {
            partnerNames.Add(await ResolveTenantNameAsync(m.MemberTenantId));
        }

        string? majorName = null;
        if (library.PrimaryMajorId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                majorName = (await _majorRepository.FindAsync(library.PrimaryMajorId.Value))?.Name;
            }
        }

        var ownerSchool = await _schoolRepository.FindByTenantIdAsync(library.OwnerTenantId);

        return new TeachingLibraryCardDto
        {
            Id = library.Id,
            Name = library.Name,
            Description = library.Description,
            CoverUrl = library.CoverUrl,
            PrimaryMajorId = library.PrimaryMajorId,
            PrimaryMajorName = majorName,
            OwnerTenantId = library.OwnerTenantId,
            OwnerTenantName = await ResolveTenantNameAsync(library.OwnerTenantId),
            OwnerSchoolLogoUrl = ownerSchool?.LogoUrl,
            Principal = library.Principal,
            Visibility = (int)library.Visibility,
            VisibilityName = _accessService.GetVisibilityName(library.Visibility),
            MemberCount = members.Count,
            PartnerNames = partnerNames,
            CourseCount = courseCount,
            ResourceCount = resourceCount,
            Scope = scope
        };
    }

    /// <summary>租户 → 中文机构名（学校档案名 → 租户扩展名 → 租户名）。</summary>
    private async Task<string> ResolveTenantNameAsync(Guid tenantId)
    {
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (!string.IsNullOrWhiteSpace(school?.Name))
        {
            return school!.Name;
        }

        var info = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (!string.IsNullOrWhiteSpace(info?.Name))
        {
            return info!.Name;
        }

        var tenant = await _tenantRepository.FindAsync(tenantId);
        return tenant?.Name ?? tenantId.ToString();
    }
}
