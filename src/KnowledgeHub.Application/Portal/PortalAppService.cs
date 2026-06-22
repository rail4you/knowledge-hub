using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.Resources;
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

        // Featured courses (latest 8 published) - populate real teacher/major names
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var rawCourses = courseQuery
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreationTime)
            .Take(8)
            .ToList();

        // Batch query student counts (cross-tenant)
        var courseIds = rawCourses.Select(c => c.Id).ToList();
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

        var featuredCourses = new List<CourseBriefDto>();
        foreach (var c in rawCourses)
        {
            var teacherName = string.Empty;
            if (c.TeacherId.HasValue)
            {
                try
                {
                    var user = await _identityUserRepository.FindAsync(c.TeacherId.Value);
                    teacherName = user?.Name ?? user?.UserName ?? string.Empty;
                }
                catch { /* user not found */ }
            }
            var majorName = string.Empty;
            if (c.MajorId.HasValue)
            {
                try
                {
                    var major = await _majorRepository.FindAsync(c.MajorId.Value);
                    majorName = major?.Name ?? string.Empty;
                }
                catch { /* major not found */ }
            }
            featuredCourses.Add(new CourseBriefDto
            {
                Id = c.Id,
                Title = c.Title,
                CoverImageUrl = c.CoverImageUrl,
                TeacherName = teacherName,
                MajorName = majorName,
                StudentCount = studentCountMap.GetValueOrDefault(c.Id, 0)
            });
        }

        // Latest materials (latest 8)
        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        var latestMaterials = resourceQuery
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreationTime)
            .Take(8)
            .Select(x => new MaterialBriefDto
            {
                Id = x.Id,
                Name = x.Name,
                FileExtension = x.FileExtension,
                DownloadCount = x.DownloadCount
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
            LatestMaterials = latestMaterials,
            LatestNews = latestNews,
            Partners = new List<PartnerBriefDto>()
        };
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
                stats.TotalResourceCount += (int)await _resourceRepository.CountAsync(x => x.TenantId == tenant.Id);
                stats.TotalMicroMajorCount += (int)await _microMajorRepository.CountAsync(
                    x => x.TenantId == tenant.Id && x.Status == MicroMajorStatus.Published);
            }
        }

        return stats;
    }

    /// <summary>
    /// 公开浏览数据：所有租户的课程/资源/微专业，游客可见，支持筛选
    /// </summary>
    [AllowAnonymous]
    public async Task<PublicBrowseDto> GetPublicBrowseAsync(Guid? tenantId, Guid? majorId, string? search, int skipCount, int maxResultCount)
    {
        // Disable multi-tenancy filter for the entire method -
        // we manually filter by tenantId below.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await BuildBrowseDataAsync(tenantId, majorId, search, skipCount, maxResultCount);
        }
    }

    private async Task<PublicBrowseDto> BuildBrowseDataAsync(Guid? tenantId, Guid? majorId, string? search, int skipCount, int maxResultCount)
    {
        var tenants = await _tenantRepository.GetListAsync();
        var tenantNames = tenants.ToDictionary(t => t.Id, t => t.Name);

        // ── 课程 ──
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var coursesFiltered = courseQuery.AsEnumerable();
        if (tenantId.HasValue)
            coursesFiltered = coursesFiltered.Where(c => c.TenantId == tenantId.Value);
        if (majorId.HasValue)
            coursesFiltered = coursesFiltered.Where(c => c.MajorId == majorId.Value);
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

        var courses = coursesFiltered
            .OrderByDescending(c => c.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(c => new PublicCourseDto
            {
                Id = c.Id,
                Title = c.Title,
                CoverImageUrl = c.CoverImageUrl,
                MajorId = c.MajorId,
                TenantId = c.TenantId ?? Guid.Empty,
                TenantName = c.TenantId.HasValue && tenantNames.ContainsKey(c.TenantId.Value) ? tenantNames[c.TenantId.Value] : null,
                StudentCount = browseStudentCountMap.GetValueOrDefault(c.Id, 0),
            }).ToList();
        var totalCourseCount = coursesFiltered.LongCount();

        // ── 资源 ──
        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        var resourcesFiltered = resourceQuery.AsEnumerable();
        if (tenantId.HasValue)
            resourcesFiltered = resourcesFiltered.Where(r => r.TenantId == tenantId.Value);
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
        var tenantOptions = tenants.Select(t => new PublicBrowseFilterOption { Id = t.Id, Name = t.Name }).ToList();
        var majorQuery = await _majorRepository.GetQueryableAsync();
        var majorOptions = majorQuery.AsEnumerable()
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
