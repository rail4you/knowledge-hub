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
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
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

        // Latest news (latest 5)
        var newsQuery = await _newsArticleRepository.GetQueryableAsync();
        var latestNews = newsQuery
            .Where(x => x.TenantId == tenantId)
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
            });
        }
        return result;
    }

    /// <summary>
    /// 获取首页公共统计数据（公开访问）
    /// </summary>
    public async Task<PublicHomeStatsDto> GetPublicHomeStatsAsync()
    {
        var tenants = await _tenantRepository.GetListAsync();
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
    /// 获取下载量最高的资源（跨所有租户），用于首页"资源排行"展示
    /// </summary>
    [AllowAnonymous]
    public async Task<List<MaterialBriefDto>> GetTopResourcesByDownloadAsync(int count = 10)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resourceQuery = await _resourceRepository.GetQueryableAsync();
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
        var tenants = await _tenantRepository.GetListAsync();
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
        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<TenantResourceSummaryDto>();

        foreach (var tenant in tenants)
        {
            var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenant.Id);

            int courseCount, resourceCount, microMajorCount;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                courseCount = (int)await _courseRepository.CountAsync(x => x.TenantId == tenant.Id);
                resourceCount = (int)await _resourceRepository.CountAsync(x => x.TenantId == tenant.Id);
                microMajorCount = (int)await _microMajorRepository.CountAsync(
                    x => x.TenantId == tenant.Id && x.Status == MicroMajorStatus.Published);
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

            result.Add(new TenantResourceSummaryDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                TenantName = tenantInfo?.Name ?? tenant.Name,
                TenantDescription = tenantInfo?.Description,
                Description = tenantInfo?.Description ?? tenant.Name + "教育资源库",
                TenantType = (int)(tenantInfo?.Type ?? 0),
                CoverImage = coverImage,
                CourseCount = (int)courseCount,
                ResourceCount = (int)resourceCount,
                MicroMajorCount = (int)microMajorCount
            });
        }

        return result;
    }
}
