using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.Resources;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
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
        Volo.Abp.Identity.IIdentityUserRepository identityUserRepository)
    {
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _newsArticleRepository = newsArticleRepository;
        _majorRepository = majorRepository;
        _tenantRepository = tenantRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _identityUserRepository = identityUserRepository;
        _microMajorRepository = microMajorRepository;
        _microMajorCourseRepository = microMajorCourseRepository;
        _microMajorResourceRepository = microMajorResourceRepository;
    }

    public async Task<PortalHomeDataDto> GetHomeDataAsync(Guid tenantId)
    {
        // Get tenant info
        var tenant = await _tenantRepository.FindAsync(tenantId);
        var tenantName = tenant?.Name ?? string.Empty;
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
                StudentCount = 0
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
                StudentCount = 0,
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
            stats.TotalCourseCount += (int)await _courseRepository.CountAsync(x => x.TenantId == tenant.Id);
            stats.TotalResourceCount += (int)await _resourceRepository.CountAsync(x => x.TenantId == tenant.Id);
            stats.TotalMicroMajorCount += (int)await _microMajorRepository.CountAsync(
                x => x.TenantId == tenant.Id && x.Status == MicroMajorStatus.Published);
        }

        return stats;
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
            var courseCount = await _courseRepository.CountAsync(x => x.TenantId == tenant.Id);
            var resourceCount = await _resourceRepository.CountAsync(x => x.TenantId == tenant.Id);
            var microMajorCount = await _microMajorRepository.CountAsync(
                x => x.TenantId == tenant.Id && x.Status == MicroMajorStatus.Published);

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
