using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Courses;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Portal;

[AllowAnonymous]
public class PortalAppService : KnowledgeHubAppService, IPortalAppService
{
    private readonly IRepository<MicroMajor, Guid> _microMajorRepository;
    private readonly IRepository<MicroMajorCourse, Guid> _microMajorCourseRepository;
    private readonly IRepository<MicroMajorResource, Guid> _microMajorResourceRepository;
    private readonly IRepository<Courses.Course, Guid> _courseRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<NewsArticle, Guid> _newsArticleRepository;
    private readonly IRepository<Majors.Major, Guid> _majorRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly Volo.Abp.Identity.IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Courses.CourseMajor, Guid> _courseMajorRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ISchoolDomainContext _schoolDomainContext;
    private readonly ICourseShareRepository _courseShareRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IDataFilter _dataFilter;

    public PortalAppService(
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorCourse, Guid> microMajorCourseRepository,
        IRepository<MicroMajorResource, Guid> microMajorResourceRepository,
        IRepository<Courses.Course, Guid> courseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<NewsArticle, Guid> newsArticleRepository,
        IRepository<Majors.Major, Guid> majorRepository,
        ITenantRepository tenantRepository,
        ITenantInfoRepository tenantInfoRepository,
        Volo.Abp.Identity.IIdentityUserRepository identityUserRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Courses.CourseMajor, Guid> courseMajorRepository,
        ISchoolRepository schoolRepository,
        ISchoolDomainContext schoolDomainContext,
        ICourseShareRepository courseShareRepository,
        IResourceLibraryRepository libraryRepository,
        IDataFilter dataFilter)
    {
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _newsArticleRepository = newsArticleRepository;
        _majorRepository = majorRepository;
        _tenantRepository = tenantRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _identityUserRepository = identityUserRepository;
        _studentCourseRepository = studentCourseRepository;
        _courseMajorRepository = courseMajorRepository;
        _schoolRepository = schoolRepository;
        _schoolDomainContext = schoolDomainContext;
        _courseShareRepository = courseShareRepository;
        _libraryRepository = libraryRepository;
        _dataFilter = dataFilter;
        _microMajorRepository = microMajorRepository;
        _microMajorCourseRepository = microMajorCourseRepository;
        _microMajorResourceRepository = microMajorResourceRepository;
    }

    public async Task<PortalHomeDataDto> GetHomeDataAsync(Guid tenantId)
    {
        // Get tenant info
        var tenant = await _tenantRepository.FindAsync(tenantId);
        var tenantName = tenant?.Name ?? string.Empty;

        // All tenant-specific queries need multi-tenancy filter disabled,
        // because when running as host/anonymous, ABP adds "TenantId IS NULL"
        // which filters out all tenant-owned data.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await BuildHomeDataAsync(tenantId, tenantName);
        }
    }

    private async Task<PortalHomeDataDto> BuildHomeDataAsync(Guid tenantId, string tenantName)
    {
        // Stats
        var courseCount = await _courseRepository.CountAsync(x => x.TenantId == tenantId);
        var resourceCount = await _resourceRepository.CountAsync(x => x.TenantId == tenantId);
        var microMajorCount = await _microMajorRepository.CountAsync(x => x.TenantId == tenantId && x.Status == MicroMajorStatus.Published);

        // Published micro-majors
        var microMajorQuery = await _microMajorRepository.GetQueryableAsync();
        var microMajors = microMajorQuery
            .Where(x => x.TenantId == tenantId && x.Status == MicroMajorStatus.Published)
            .OrderByDescending(x => x.CreationTime)
            .Take(4)
            .Select(x => new MicroMajorBriefDto
            {
                Id = x.Id,
                Title = x.Title,
                CoverImageUrl = x.CoverImageUrl,
                CourseCount = 0
            })
            .ToList();

        // Fill course counts for micro-majors
        foreach (var mm in microMajors)
        {
            mm.CourseCount = (int)await _microMajorCourseRepository.CountAsync(x => x.MicroMajorId == mm.Id);
        }

        // 首页"精选课程"：仅展示已发布且标记为推荐的课程，
        // 与 Domain 层注释"学生端推荐课程 Tab 只显示开启此开关的已发布课程"保持一致。
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var rawFeaturedCourses = courseQuery
            .Where(x => x.TenantId == tenantId
                && x.Status == CourseStatus.Published
                && x.IsRecommended)
            .OrderByDescending(x => x.CreationTime)
            .Take(8)
            .ToList();

        // 租户首页"学历课程体系"：取该租户下所有已发布课程（含未标记推荐的），
        // 不按 IsRecommended 过滤，否则租户刚建课（未标记推荐）时该区域会一片空白。
        var rawPublishedCourses = courseQuery
            .Where(x => x.TenantId == tenantId
                && x.Status == CourseStatus.Published)
            .OrderByDescending(x => x.CreationTime)
            .Take(8)
            .ToList();

        // 「共享到本机构」的课程（如共享到社会学校的课）：
        // 本租户自己一门课都没有时，内容全靠共享 —— 不并进来首页/租户首页就是空的。
        var sharedCourseIds = await GetSharedCourseIdsAsync(tenantId);
        if (sharedCourseIds.Count > 0)
        {
            var sharedCourses = courseQuery
                .Where(x => sharedCourseIds.Contains(x.Id) && x.Status == CourseStatus.Published)
                .OrderByDescending(x => x.CreationTime)
                .Take(8)
                .ToList();

            var ownIds = rawPublishedCourses.Select(c => c.Id).ToHashSet();
            rawPublishedCourses = rawPublishedCourses
                .Concat(sharedCourses.Where(c => !ownIds.Contains(c.Id)))
                .Take(8)
                .ToList();

            var featuredOwnIds = rawFeaturedCourses.Select(c => c.Id).ToHashSet();
            rawFeaturedCourses = rawFeaturedCourses
                .Concat(sharedCourses.Where(c => c.IsRecommended && !featuredOwnIds.Contains(c.Id)))
                .Take(8)
                .ToList();
        }

        // Batch query student counts (cross-tenant) — 两份课程合在一起一次查询，避免重复
        var courseIds = rawFeaturedCourses.Select(c => c.Id)
            .Concat(rawPublishedCourses.Select(c => c.Id))
            .Distinct()
            .ToList();
        Dictionary<Guid, int> studentCountMap;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            var scCounts = await AsyncExecuter.ToListAsync(
                scQuery.Where(sc => courseIds.Contains(sc.CourseId) && sc.Status != StudentCourseStatus.Dropped)
                    .GroupBy(sc => sc.CourseId)
                    .Select(g => new { CourseId = g.Key, Count = g.Count() }));
            studentCountMap = scCounts.ToDictionary(x => x.CourseId, x => x.Count);
        }

        // Batch query total student count for stats (cross-tenant)
        int totalStudentCount;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            var distinctStudentIds = await AsyncExecuter.ToListAsync(
                scQuery.Where(sc => sc.Status != StudentCourseStatus.Dropped)
                    .Select(sc => sc.StudentId)
                    .Distinct());
            totalStudentCount = distinctStudentIds.Count;
        }

        // 填充两份课程的展示信息（teacher / major / studentCount），缓存避免重复查 identity/major
        var teacherNameCache = new Dictionary<Guid, string>();
        var majorNameCache = new Dictionary<Guid, string>();
        var featuredCourses = await BuildCourseBriefListAsync(rawFeaturedCourses, studentCountMap, teacherNameCache, majorNameCache);
        var publishedCourses = await BuildCourseBriefListAsync(rawPublishedCourses, studentCountMap, teacherNameCache, majorNameCache);

        // Latest materials (latest 8)
        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        var latestMaterials = resourceQuery
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.Status >= ResourceStatus.SchoolApproved)
            .OrderByDescending(x => x.CreationTime)
            .Take(8)
            .Select(x => new MaterialBriefDto
            {
                Id = x.Id,
                Name = x.Name,
                FileExtension = x.FileExtension,
                DownloadCount = x.DownloadCount,
                FileSize = x.FileSize ?? 0,
                OriginalFileName = x.OriginalFileName
            })
            .ToList();

        // Latest news (latest 5, 仅已发布：首页跨租户聚合/预览只对已发布内容开放)
        var newsQuery = await _newsArticleRepository.GetQueryableAsync();
        var latestNews = newsQuery
            .Where(x => x.TenantId == tenantId && x.Status == NewsArticleStatus.Published)
            .OrderByDescending(x => x.CreationTime)
            .Take(5)
            .Select(x => new NewsBriefDto
            {
                Id = x.Id,
                Title = x.Title,
                PublishedAt = x.PublishedAt
            })
            .ToList();

        return new PortalHomeDataDto
        {
            TenantInfo = new TenantBriefDto { Id = tenantId, Name = tenantName },
            Stats = new PortalStatsDto
            {
                CourseCount = (int)courseCount,
                ResourceCount = (int)resourceCount,
                StudentCount = totalStudentCount,
                MicroMajorCount = microMajorCount
            },
            MicroMajors = microMajors,
            FeaturedCourses = featuredCourses,
            PublishedCourses = publishedCourses,
            LatestMaterials = latestMaterials,
            LatestNews = latestNews,
            Partners = new List<PartnerBriefDto>()
        };
    }

    /// <summary>
    /// 填充课程摘要列表的展示信息（teacherName / majorName / studentCount / difficulty），
    /// 使用缓存避免对同一 teacher/major 重复查库。
    /// </summary>
    /// <summary>
    /// 「共享到本机构」的课程 Id 列表。
    ///
    /// <para>共享记录按源租户隔离（<c>TenantId = SourceTenantId</c>），查询目标租户时
    /// 必须禁用租户过滤器，按 <c>TargetTenantId</c> 精确匹配 —— 与
    /// <c>CourseShareAppService.GetSharedToMeAsync</c> 同一套口径。</para>
    /// </summary>
    private async Task<List<Guid>> GetSharedCourseIdsAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var shareQuery = await _courseShareRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                shareQuery.Where(s => s.TargetTenantId == tenantId).Select(s => s.CourseId).Distinct());
        }
    }

    private async Task<List<CourseBriefDto>> BuildCourseBriefListAsync(
        List<Courses.Course> courses,
        Dictionary<Guid, int> studentCountMap,
        Dictionary<Guid, string> teacherNameCache,
        Dictionary<Guid, string> majorNameCache)
    {
        // 批量加载课程→专业关联（主专业排第一），一次查出全部专业名
        var courseIds = courses.Select(c => c.Id).Distinct().ToList();
        var linksByCourse = new Dictionary<Guid, List<Courses.CourseMajor>>();
        var majorNamesById = new Dictionary<Guid, string>();
        if (courseIds.Count > 0)
        {
            var linkQuery = await _courseMajorRepository.GetQueryableAsync();
            var links = linkQuery.Where(x => courseIds.Contains(x.CourseId)).ToList();
            linksByCourse = links
                .GroupBy(x => x.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreationTime).ToList());
            var majorIds = links.Select(x => x.MajorId).Distinct().ToList();
            if (majorIds.Count > 0)
            {
                var majorQuery = await _majorRepository.GetQueryableAsync();
                majorNamesById = majorQuery
                    .Where(x => majorIds.Contains(x.Id))
                    .ToDictionary(x => x.Id, x => x.Name);
            }
        }

        var result = new List<CourseBriefDto>(courses.Count);
        foreach (var c in courses)
        {
            var teacherName = string.Empty;
            if (c.TeacherId.HasValue)
            {
                if (!teacherNameCache.TryGetValue(c.TeacherId.Value, out teacherName))
                {
                    try
                    {
                        var u = await _identityUserRepository.FindAsync(c.TeacherId.Value);
                        teacherName = u?.Name ?? u?.UserName ?? string.Empty;
                    }
                    catch { /* user not found */ }
                    teacherNameCache[c.TeacherId.Value] = teacherName;
                }
            }
            List<Guid> orderedMajorIds;
            if (linksByCourse.TryGetValue(c.Id, out var courseLinks) && courseLinks.Count > 0)
            {
                orderedMajorIds = courseLinks.Select(x => x.MajorId).Distinct().ToList();
            }
            else if (c.MajorId.HasValue)
            {
                // 兼容老数据：关联表无记录时回退到课程主专业字段
                orderedMajorIds = new List<Guid> { c.MajorId.Value };
            }
            else
            {
                orderedMajorIds = new List<Guid>();
            }
            var orderedMajorNames = new List<string>();
            foreach (var mid in orderedMajorIds)
            {
                if (majorNamesById.TryGetValue(mid, out var nm) && !string.IsNullOrWhiteSpace(nm))
                {
                    orderedMajorNames.Add(nm);
                    continue;
                }
                // 回退到原单查缓存口径
                if (!majorNameCache.TryGetValue(mid, out var cached))
                {
                    try
                    {
                        var m = await _majorRepository.FindAsync(mid);
                        cached = m?.Name ?? string.Empty;
                    }
                    catch { cached = string.Empty; }
                    majorNameCache[mid] = cached;
                }
                if (!string.IsNullOrWhiteSpace(cached)) orderedMajorNames.Add(cached);
            }
            var majorName = orderedMajorNames.Count > 0 ? orderedMajorNames[0] : string.Empty;
            result.Add(new CourseBriefDto
            {
                Id = c.Id,
                Title = c.Title,
                CoverImageUrl = c.CoverImageUrl,
                TeacherName = teacherName,
                MajorName = majorName,
                MajorNames = orderedMajorNames,
                MajorIds = orderedMajorIds,
                StudentCount = studentCountMap.GetValueOrDefault(c.Id, 0),
                Difficulty = c.Difficulty,
                IsShared = c.IsShared,
                SemesterHours = c.SemesterHours ?? 0,
                Credits = c.Credits ?? 0,
            });
        }
        return result;
    }

    /// <summary>
    /// 获取首页公共统计数据（公开访问）
    /// </summary>
    public async Task<PublicHomeStatsDto> GetPublicHomeStatsAsync()
    {
        // 域名绑定：学校域名下只统计本校；平台域名下统计全平台（维持现状）。
        var tenants = await GetScopeTenantsAsync();
        var stats = new PublicHomeStatsDto
        {
            TenantCount = tenants.Count
        };

        foreach (var tenant in tenants)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                stats.TotalCourseCount += (int)await _courseRepository.CountAsync(x => x.TenantId == tenant.Id);
                // 只统计已上架（校级/联盟级审核通过）的资源：草稿、待审核、驳回、隐藏的不应计入公开数据，
                // 否则首页/资源库顶部的总数会与实际可见列表（LeagueApproved）对不上。
                stats.TotalResourceCount += (int)await _resourceRepository.CountAsync(
                    x => x.TenantId == tenant.Id && x.Status >= ResourceStatus.SchoolApproved);
                stats.TotalMicroMajorCount += (int)await _microMajorRepository.CountAsync(
                    x => x.TenantId == tenant.Id && x.Status == MicroMajorStatus.Published);
            }
        }

        return stats;
    }

    /// <summary>
    /// 域名绑定下的可见租户范围。
    ///
    /// <para>
    /// 学校域名（请求落在租户上下文）→ 只包含当前租户，门户只显示本校内容；
    /// 平台域名（host 上下文）→ 全部租户，维持平台级全景展示。
    /// </para>
    ///
    /// <para>
    /// 判定依据是「租户是否由学校域名解析而来」，而不是「有没有租户上下文」。
    /// 平台域名下浏览器可能残留 <c>__tenant</c> cookie（登录页选机构时写入），
    /// 那会让 ABP 解析出租户上下文 —— 若据此收窄，平台门户就只剩一所学校，
    /// 首页资源库卡片 / 公开浏览再也看不到其他学校的内容。
    /// </para>
    /// </summary>
    private async Task<List<Tenant>> GetScopeTenantsAsync()
    {
        if (CurrentTenant.Id.HasValue && await _schoolDomainContext.IsCurrentHostSchoolDomainAsync())
        {
            var current = await _tenantRepository.FindAsync(CurrentTenant.Id.Value);
            return current == null ? new List<Tenant>() : new List<Tenant> { current };
        }

        return await _tenantRepository.GetListAsync();
    }

    /// <summary>
    /// 获取下载量最高的资源（跨所有租户），用于首页"资源排行"展示
    /// </summary>
    [AllowAnonymous]
    public async Task<List<MaterialBriefDto>> GetTopResourcesByDownloadAsync(int count = 10)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resourceQuery = await _resourceRepository.GetQueryableAsync();
            // 域名绑定：学校域名下只排本校资源
            if (CurrentTenant.Id.HasValue)
            {
                var currentTenantId = CurrentTenant.Id.Value;
                resourceQuery = resourceQuery.Where(x => x.TenantId == currentTenantId);
            }

            var topResources = resourceQuery
                .Where(x => x.Status >= ResourceStatus.SchoolApproved)
                .OrderByDescending(x => x.DownloadCount)
                .Take(count)
                .Select(x => new MaterialBriefDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    FileExtension = x.FileExtension,
                    DownloadCount = x.DownloadCount,
                    FileSize = x.FileSize ?? 0,
                    OriginalFileName = x.OriginalFileName
                })
                .ToList();
            return topResources;
        }
    }

    /// <summary>
    /// 公开浏览数据：所有租户的课程/资源/微专业，游客可见，支持筛选
    /// </summary>
    [AllowAnonymous]
    public async Task<PublicBrowseDto> GetPublicBrowseAsync(Guid? tenantId, Guid? majorId, string? search, int skipCount, int maxResultCount, bool? onlyPublicCourses = null)
    {
        // Disable multi-tenancy filter for the entire method -
        // we manually filter by tenantId below.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await BuildBrowseDataAsync(tenantId, majorId, search, skipCount, maxResultCount, onlyPublicCourses);
        }
    }

    private async Task<PublicBrowseDto> BuildBrowseDataAsync(Guid? tenantId, Guid? majorId, string? search, int skipCount, int maxResultCount, bool? onlyPublicCourses = null)
    {
        // 域名绑定：学校域名下强制限定为当前租户（忽略传入的 tenantId），
        // 平台域名下按传入的 tenantId 过滤（现状不变）。
        if (CurrentTenant.Id.HasValue)
        {
            tenantId = CurrentTenant.Id.Value;
        }

        var tenants = await GetScopeTenantsAsync();
        var tenantInfos = (await _tenantInfoRepository.GetListAsync()).ToDictionary(ti => ti.TenantId, ti => ti.Name);
        var tenantNames = tenants.ToDictionary(t => t.Id, t => tenantInfos.GetValueOrDefault(t.Id, t.Name) ?? t.Name);

        // ── 课程 ──
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var coursesFiltered = courseQuery.AsEnumerable();
        if (tenantId.HasValue)
            coursesFiltered = coursesFiltered.Where(c => c.TenantId == tenantId.Value);
        var browseLinkQuery = await _courseMajorRepository.GetQueryableAsync();
        var browseLinkedAny = browseLinkQuery.Select(x => x.CourseId).ToHashSet();
        if (onlyPublicCourses == true)
        {
            // 只看公共课（无任何专业归属）
            coursesFiltered = coursesFiltered.Where(c => !browseLinkedAny.Contains(c.Id));
        }
        else if (majorId.HasValue)
        {
            // 严格按专业筛选：命中该专业（含兼属）才返回。
            // 公共课（无任何专业归属）请用“公共课”选项（onlyPublicCourses）单独筛选，不在此混入。
            var linkedMatched = browseLinkQuery.Where(x => x.MajorId == majorId.Value).Select(x => x.CourseId).ToHashSet();
            coursesFiltered = coursesFiltered.Where(c =>
                (c.MajorId.HasValue && c.MajorId.Value == majorId.Value) ||
                linkedMatched.Contains(c.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
            coursesFiltered = coursesFiltered.Where(c => (c.Title ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                || (c.Description ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
        var allCourseIds = coursesFiltered.Select(c => c.Id).ToList();
        Dictionary<Guid, int> browseStudentCountMap;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            var scCounts = await AsyncExecuter.ToListAsync(
                scQuery.Where(sc => allCourseIds.Contains(sc.CourseId) && sc.Status != StudentCourseStatus.Dropped)
                    .GroupBy(sc => sc.CourseId)
                    .Select(g => new { CourseId = g.Key, Count = g.Count() }));
            browseStudentCountMap = scCounts.ToDictionary(x => x.CourseId, x => x.Count);
        }

        var pagedCourses = coursesFiltered
            .OrderByDescending(c => c.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();
        // 批量解析本页课程的专业归属（主专业排第一）与教师名，供卡片展示
        var pagedCourseIds = pagedCourses.Select(c => c.Id).Distinct().ToList();
        var majorLinksByCourse = new Dictionary<Guid, List<Guid>>();
        var browseMajorNames = new Dictionary<Guid, string>();
        if (pagedCourseIds.Count > 0)
        {
            var pagedLinks = browseLinkQuery.Where(x => pagedCourseIds.Contains(x.CourseId)).ToList();
            majorLinksByCourse = pagedLinks
                .GroupBy(x => x.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreationTime).Select(x => x.MajorId).Distinct().ToList());
            var pagedMajorIds = pagedLinks.Select(x => x.MajorId).Distinct().ToList();
            if (pagedMajorIds.Count > 0)
            {
                var browseMajorQuery = await _majorRepository.GetQueryableAsync();
                browseMajorNames = browseMajorQuery
                    .Where(x => pagedMajorIds.Contains(x.Id))
                    .ToDictionary(x => x.Id, x => x.Name);
            }
        }
        var browseTeacherNames = new Dictionary<Guid, string>();
        foreach (var tid in pagedCourses.Where(c => c.TeacherId.HasValue).Select(c => c.TeacherId!.Value).Distinct().ToList())
        {
            try
            {
                var u = await _identityUserRepository.FindAsync(tid);
                browseTeacherNames[tid] = u?.Name ?? u?.UserName ?? string.Empty;
            }
            catch { browseTeacherNames[tid] = string.Empty; }
        }
        var courses = pagedCourses
            .Select(c => {
                List<Guid> orderedIds;
                if (majorLinksByCourse.TryGetValue(c.Id, out var courseLinks) && courseLinks.Count > 0)
                    orderedIds = courseLinks;
                else if (c.MajorId.HasValue)
                    orderedIds = new List<Guid> { c.MajorId.Value };
                else
                    orderedIds = new List<Guid>();
                var orderedNames = orderedIds
                    .Select(id => browseMajorNames.GetValueOrDefault(id))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Cast<string>()
                    .ToList();
                var primaryId = orderedIds.Count > 0 ? orderedIds[0] : c.MajorId;
                return new PublicCourseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    CoverImageUrl = c.CoverImageUrl,
                    Description = c.Description,
                    TeacherName = c.TeacherId.HasValue ? browseTeacherNames.GetValueOrDefault(c.TeacherId.Value, string.Empty) : null,
                    MajorId = primaryId,
                    MajorName = orderedNames.Count > 0 ? orderedNames[0] : null,
                    MajorIds = orderedIds,
                    MajorNames = orderedNames,
                    TenantId = c.TenantId ?? Guid.Empty,
                    TenantName = c.TenantId.HasValue && tenantNames.ContainsKey(c.TenantId.Value) ? tenantNames[c.TenantId.Value] : null,
                    StudentCount = browseStudentCountMap.GetValueOrDefault(c.Id, 0),
                    IsShared = c.IsShared,
                    SemesterHours = c.SemesterHours ?? 0,
                    Credits = c.Credits ?? 0,
                };
            }).ToList();
        var totalCourseCount = coursesFiltered.LongCount();

        // ── 资源 ──
        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        var resourcesFiltered = resourceQuery.AsEnumerable();
        if (tenantId.HasValue)
            resourcesFiltered = resourcesFiltered.Where(r => r.TenantId == tenantId.Value);
        // 公开浏览只展示已审核通过的资源
        resourcesFiltered = resourcesFiltered.Where(r => r.Status >= ResourceStatus.SchoolApproved);
        if (!string.IsNullOrWhiteSpace(search))
            resourcesFiltered = resourcesFiltered.Where(r => (r.Name ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
        var resources = resourcesFiltered
            .OrderByDescending(r => r.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(r => new PublicResourceDto
            {
                Id = r.Id,
                Name = r.Name,
                FileExtension = r.FileExtension,
                DownloadCount = r.DownloadCount,
                TenantId = r.TenantId ?? Guid.Empty,
                TenantName = r.TenantId.HasValue && tenantNames.ContainsKey(r.TenantId.Value) ? tenantNames[r.TenantId.Value] : null,
                FileSize = r.FileSize ?? 0,
                OriginalFileName = r.OriginalFileName,
            }).ToList();
        var totalResourceCount = resourcesFiltered.LongCount();

        // ── 微专业 ──
        var mmQuery = await _microMajorRepository.GetQueryableAsync();
        var mmsFiltered = mmQuery.Where(m => m.Status == MicroMajorStatus.Published).AsEnumerable();
        if (tenantId.HasValue)
            mmsFiltered = mmsFiltered.Where(m => m.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(search))
            mmsFiltered = mmsFiltered.Where(m => (m.Title ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
        var microMajorList = mmsFiltered
            .OrderByDescending(m => m.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();

        // 统计每个微专业关联的课程数（修复首页微专业显示 0 课程的 bug）
        var microMajorIds = microMajorList.Select(m => m.Id).ToList();
        var microMajorCourseCountMap = new Dictionary<Guid, int>();
        if (microMajorIds.Count > 0)
        {
            var links = await _microMajorCourseRepository.GetListAsync(x => microMajorIds.Contains(x.MicroMajorId));
            microMajorCourseCountMap = links
                .GroupBy(x => x.MicroMajorId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var microMajors = microMajorList
            .Select(m => new PublicMicroMajorDto
            {
                Id = m.Id,
                Title = m.Title,
                CoverImageUrl = m.CoverImageUrl,
                CourseCount = microMajorCourseCountMap.GetValueOrDefault(m.Id, 0),
                TenantId = m.TenantId ?? Guid.Empty,
                TenantName = m.TenantId.HasValue && tenantNames.ContainsKey(m.TenantId.Value) ? tenantNames[m.TenantId.Value] : null,
            }).ToList();
        var totalMicroMajorCount = mmsFiltered.LongCount();

        // ── 筛选选项 ──
        var tenantOptions = tenants.Select(t => new PublicBrowseFilterOption { Id = t.Id, Name = tenantInfos.GetValueOrDefault(t.Id, t.Name) ?? t.Name }).ToList();
        var majorQuery = await _majorRepository.GetQueryableAsync();
        // 专业是租户级数据：选中租户时只返回该租户的专业，避免选串到其他租户的专业
        var majorOptions = majorQuery.AsEnumerable()
            .Where(m => !tenantId.HasValue || m.TenantId == tenantId.Value)
            .Select(m => new PublicBrowseFilterOption { Id = m.Id, Name = m.Name }).ToList();

        return new PublicBrowseDto
        {
            Courses = courses,
            Resources = resources,
            MicroMajors = microMajors,
            Tenants = tenantOptions,
            Majors = majorOptions,
            TotalCourseCount = totalCourseCount,
            TotalResourceCount = totalResourceCount,
            TotalMicroMajorCount = totalMicroMajorCount,
        };
    }

    /// <summary>
    /// 获取所有租户的资源库摘要列表（公开访问）
    /// 用于主页展示所有租户，包含 TenantInfo 的扩展信息
    /// </summary>
    public async Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync()
    {
        // 域名绑定：学校域名下只返回本校（首页资源库卡片只显示自己学校）
        var tenants = await GetScopeTenantsAsync();
        var result = new List<TenantResourceSummaryDto>();

        // 学校档案：徽标与学校名称（首页资源库卡片展示，点击跳学校简介）
        var schoolMap = (await _schoolRepository.GetQueryableAsync())
            .ToDictionary(x => x.TenantId, x => x);

        foreach (var tenant in tenants)
        {
            var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenant.Id);
            schoolMap.TryGetValue(tenant.Id, out var school);

            int courseCount, resourceCount, microMajorCount;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                courseCount = (int)await _courseRepository.CountAsync(x => x.TenantId == tenant.Id);
                resourceCount = (int)await _resourceRepository.CountAsync(x => x.TenantId == tenant.Id);
                microMajorCount = (int)await _microMajorRepository.CountAsync(
                    x => x.TenantId == tenant.Id && x.Status == MicroMajorStatus.Published);
            }

            // 共享到本机构的课程也要计入（如共享到社会学校的课）：
            // 社会学校自己没有课，卡片上的「N 门课程」不能显示 0。
            var sharedCourseIds = await GetSharedCourseIdsAsync(tenant.Id);
            if (sharedCourseIds.Count > 0)
            {
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    var sharedPublished = await _courseRepository.CountAsync(
                        x => sharedCourseIds.Contains(x.Id) && x.Status == CourseStatus.Published);
                    courseCount += (int)sharedPublished;
                }
            }

            var coverImage = string.Empty;
            if (tenantInfo?.CoverImages != null)
            {
                try
                {
                    var images = JsonSerializer.Deserialize<List<string>>(tenantInfo.CoverImages);
                    if (images != null && images.Count > 0)
                        coverImage = images[0];
                }
                catch { }
            }

            // 学校档案名若只是租户 slug 的原样回显（自动开卡默认名），不算展示名，
            // 继续用专业库配置的中文名 —— 需求「机构名称要用中文」。
            var effectiveSchoolName = school != null
                                      && !string.Equals(school.Name, tenant.Name, StringComparison.Ordinal)
                ? school.Name
                : null;

            result.Add(new TenantResourceSummaryDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                // 优先用学校档案名称，其次专业库展示名，最后租户名
                TenantName = effectiveSchoolName ?? tenantInfo?.Name ?? tenant.Name,
                // 资源库名称：专业库（TenantInfo）配置的库名，没有则退回租户名
                LibraryName = tenantInfo?.Name ?? tenant.Name,
                // 建设单位：学校档案名（需求：资源库详情与卡片要展示建设单位）
                SchoolName = effectiveSchoolName,
                TenantDescription = tenantInfo?.Description,
                Description = tenantInfo?.Description ?? $"{effectiveSchoolName ?? tenantInfo?.Name ?? tenant.Name}教育资源库",
                TenantType = (int)(tenantInfo?.Type ?? 0),
                CoverImage = coverImage,
                // 学校徽标（需求：首页显示学校徽标和名称）
                LogoUrl = school?.LogoUrl,
                IndustryField = tenantInfo?.Description,
                CourseCount = (int)courseCount,
                ResourceCount = (int)resourceCount,
                MicroMajorCount = (int)microMajorCount
            });
        }

        return result;
    }

    // ── 公开资源库（多校共建专业库）─────────────────────────────

    /// <summary>
    /// 获取公开的资源库列表（跨租户）。
    ///
    /// <para>
    /// 门户「共建共享资源库」列表与首页重点共建卡片都读这里。
    /// 只暴露 <c>IsPublic=true</c> 且状态为「已发布」的库 —— 草稿/归档库不应出现在公开门户。
    /// </para>
    /// </summary>
    public async Task<List<PublicResourceLibraryDto>> GetPublicLibraryListAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _libraryRepository.GetQueryableAsync();
            var libraries = await AsyncExecuter.ToListAsync(query
                .Where(x => x.IsPublic && x.Status == ResourceLibraryStatus.Active)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreationTime));

            var result = new List<PublicResourceLibraryDto>();
            foreach (var library in libraries)
            {
                result.Add(await BuildPublicLibraryDtoAsync(library, false));
            }

            return result;
        }
    }

    /// <summary>
    /// 获取单个公开资源库的详情（含参建单位、课程、资源、微专业、师资与统计）。
    /// </summary>
    public async Task<PublicResourceLibraryDetailDto> GetPublicLibraryAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var library = await _libraryRepository.FindAsync(id);
            if (library == null || !library.IsPublic || library.Status != ResourceLibraryStatus.Active)
            {
                throw new UserFriendlyException("资源库不存在或未发布。");
            }

            return (PublicResourceLibraryDetailDto)await BuildPublicLibraryDtoAsync(library, true);
        }
    }

    /// <summary>
    /// 组装公开资源库 DTO。
    /// </summary>
    /// <param name="library">资源库实体。</param>
    /// <param name="includeDetail">true 时加载课程/资源/微专业/师资明细。</param>
    private async Task<PublicResourceLibraryDto> BuildPublicLibraryDtoAsync(ResourceLibrary library, bool includeDetail)
    {
        var members = await _libraryRepository.GetMembersAsync(library.Id);

        // 主建学校：优先学校档案名（中文校名），其次专业库展示名，最后租户名
        string? ownerName = null;
        string? ownerLogo = null;
        var ownerTenant = await _tenantRepository.FindAsync(library.OwnerTenantId);
        var ownerSchool = await _schoolRepository.FindByTenantIdAsync(library.OwnerTenantId);
        if (ownerSchool != null && !string.IsNullOrWhiteSpace(ownerSchool.Name)
            && !string.Equals(ownerSchool.Name, ownerTenant?.Name, StringComparison.Ordinal))
        {
            ownerName = ownerSchool.Name;
        }
        ownerLogo = ownerSchool?.LogoUrl;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            var ownerInfo = await _tenantInfoRepository.FindByTenantIdAsync(library.OwnerTenantId);
            ownerName = !string.IsNullOrWhiteSpace(ownerInfo?.Name) ? ownerInfo.Name : ownerTenant?.Name;
        }

        // 主建专业名称
        string? majorName = null;
        if (library.PrimaryMajorId.HasValue)
        {
            var major = await _majorRepository.FindAsync(library.PrimaryMajorId.Value);
            majorName = major?.Name;
        }

        // 成员显示名：优先学校档案名
        var memberDtos = new List<PublicLibraryMemberDto>();
        var partnerNames = new List<string>();
        foreach (var m in members)
        {
            var displayName = m.MemberTenantName;
            var memberSchool = await _schoolRepository.FindByTenantIdAsync(m.MemberTenantId);
            if (memberSchool != null && !string.IsNullOrWhiteSpace(memberSchool.Name))
            {
                displayName = memberSchool.Name;
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = m.MemberTenantId.ToString();
            }

            memberDtos.Add(new PublicLibraryMemberDto
            {
                MemberTenantId = m.MemberTenantId,
                MemberTenantName = displayName,
                Role = (int)m.Role,
                JoinedTime = m.JoinedTime
            });

            if (m.Role == ResourceLibraryMemberRole.Partner)
            {
                partnerNames.Add(displayName);
            }
        }

        // 库内内容统计 + 明细
        var courseCount = 0;
        var resourceCount = 0;
        var microMajorCount = 0;
        var studentCount = 0;
        var courses = new List<PublicLibraryCourseDto>();
        var resources = new List<PublicLibraryResourceDto>();
        var microMajors = new List<PublicLibraryMicroMajorDto>();
        var teachers = new List<PublicLibraryTeacherDto>();

        var courseQuery = await _courseRepository.GetQueryableAsync();
        // 库内课程 = ①显式挂进本库的课程 ②主建学校该专业下的已发布课程。
        // 只按 LibraryId 过滤会让刚建库、尚未逐条挂载的库内容全空，
        // 门户上会看到一个"什么都没有"的资源库 —— 所以这里按专业维度兜底。
        var libraryCourses = await AsyncExecuter.ToListAsync(courseQuery
            .Where(x => x.Status == CourseStatus.Published)
            .Where(x => x.LibraryId == library.Id
                        || (x.TenantId == library.OwnerTenantId
                            && (!library.PrimaryMajorId.HasValue || x.MajorId == library.PrimaryMajorId.Value)))
            .OrderByDescending(x => x.CreationTime));

        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        var libraryResources = await AsyncExecuter.ToListAsync(resourceQuery
            .Where(x => x.Status >= ResourceStatus.SchoolApproved)
            .Where(x => x.LibraryId == library.Id
                        || (x.TenantId == library.OwnerTenantId
                            && (!library.PrimaryMajorId.HasValue || x.MajorId == library.PrimaryMajorId.Value)))
            .OrderByDescending(x => x.CreationTime));

        // 主建学校的已发布微专业（作为「实训资源」等栏目内容来源）
        var microMajorQuery = await _microMajorRepository.GetQueryableAsync();
        var libraryMicroMajors = await AsyncExecuter.ToListAsync(microMajorQuery
            .Where(x => x.TenantId == library.OwnerTenantId && x.Status == MicroMajorStatus.Published)
            .OrderByDescending(x => x.CreationTime));

        courseCount = libraryCourses.Count;
        resourceCount = libraryResources.Count;
        microMajorCount = libraryMicroMajors.Count;

        // 课程学习人数
        var studentCountMap = new Dictionary<Guid, int>();
        if (libraryCourses.Count > 0)
        {
            var courseIds = libraryCourses.Select(c => c.Id).ToList();
            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            var scCounts = await AsyncExecuter.ToListAsync(
                scQuery.Where(sc => courseIds.Contains(sc.CourseId) && sc.Status != StudentCourseStatus.Dropped)
                    .GroupBy(sc => sc.CourseId)
                    .Select(g => new { CourseId = g.Key, Count = g.Count() }));
            studentCountMap = scCounts.ToDictionary(x => x.CourseId, x => x.Count);
            studentCount = studentCountMap.Values.Sum();
        }

        if (includeDetail)
        {
            var teacherNameCache = new Dictionary<Guid, string>();
            var majorNameCache = new Dictionary<Guid, string>();
            var courseBriefs = await BuildCourseBriefListAsync(libraryCourses, studentCountMap, teacherNameCache, majorNameCache);
            foreach (var brief in courseBriefs)
            {
                var raw = libraryCourses.First(c => c.Id == brief.Id);
                courses.Add(new PublicLibraryCourseDto
                {
                    Id = brief.Id,
                    Title = brief.Title,
                    CoverImageUrl = brief.CoverImageUrl,
                    TeacherName = brief.TeacherName,
                    MajorName = brief.MajorName,
                    Description = raw.Description,
                    StudentCount = brief.StudentCount,
                    SemesterHours = raw.SemesterHours ?? 0,
                    Credits = raw.Credits ?? 0,
                    Difficulty = brief.Difficulty
                });
            }

            foreach (var r in libraryResources)
            {
                resources.Add(new PublicLibraryResourceDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    FileExtension = r.FileExtension,
                    OriginalFileName = r.OriginalFileName,
                    FileSize = r.FileSize ?? 0,
                    DownloadCount = r.DownloadCount,
                    CoverUrl = null,
                    ResourceTypeName = ResourceTypeLabel(r.ResourceType),
                    CreationTime = r.CreationTime
                });
            }

            foreach (var mm in libraryMicroMajors)
            {
                var mmCourseCount = await _microMajorCourseRepository.CountAsync(x => x.MicroMajorId == mm.Id);
                var mmResourceCount = await _microMajorResourceRepository.CountAsync(x => x.MicroMajorId == mm.Id);
                microMajors.Add(new PublicLibraryMicroMajorDto
                {
                    Id = mm.Id,
                    Title = mm.Title,
                    Summary = mm.Summary,
                    Description = mm.Description,
                    CoverImageUrl = mm.CoverImageUrl,
                    CollaborationUnit = mm.CollaborationUnit,
                    CourseCount = (int)mmCourseCount,
                    ResourceCount = (int)mmResourceCount
                });
            }

            // 师资：按课程授课教师聚合
            teachers = courseBriefs
                .Where(c => !string.IsNullOrWhiteSpace(c.TeacherName))
                .GroupBy(c => c.TeacherName!)
                .Select(g => new PublicLibraryTeacherDto
                {
                    Name = g.Key,
                    CourseCount = g.Count()
                })
                .OrderByDescending(t => t.CourseCount)
                .ThenBy(t => t.Name)
                .ToList();
        }

        var result = new PublicResourceLibraryDto
        {
            Id = library.Id,
            Name = library.Name,
            Description = library.Description,
            CoverUrl = library.CoverUrl,
            PrimaryMajorId = library.PrimaryMajorId,
            PrimaryMajorName = majorName,
            OwnerTenantId = library.OwnerTenantId,
            OwnerTenantName = ownerName,
            OwnerSchoolLogoUrl = ownerLogo,
            Principal = library.Principal,
            MemberCount = memberDtos.Count,
            PartnerNames = partnerNames,
            CourseCount = courseCount,
            ResourceCount = resourceCount,
            MicroMajorCount = microMajorCount,
            Status = (int)library.Status
        };

        if (!includeDetail)
        {
            return result;
        }

        return new PublicResourceLibraryDetailDto
        {
            Id = result.Id,
            Name = result.Name,
            Description = result.Description,
            CoverUrl = result.CoverUrl,
            PrimaryMajorId = result.PrimaryMajorId,
            PrimaryMajorName = result.PrimaryMajorName,
            OwnerTenantId = result.OwnerTenantId,
            OwnerTenantName = result.OwnerTenantName,
            OwnerSchoolLogoUrl = result.OwnerSchoolLogoUrl,
            Principal = result.Principal,
            MemberCount = result.MemberCount,
            PartnerNames = result.PartnerNames,
            CourseCount = result.CourseCount,
            ResourceCount = result.ResourceCount,
            MicroMajorCount = result.MicroMajorCount,
            Status = result.Status,
            Members = memberDtos,
            Courses = courses,
            Resources = resources,
            MicroMajors = microMajors,
            Teachers = teachers,
            StudentCount = studentCount
        };
    }

    /// <summary>资源类型的中文展示名。</summary>
    private static string ResourceTypeLabel(ResourceType type)
    {
        return type switch
        {
            ResourceType.Document => "文档",
            ResourceType.Video => "视频",
            ResourceType.Audio => "音频",
            ResourceType.Image => "图片",
            ResourceType.PPT => "课件",
            _ => "资料"
        };
    }
}
