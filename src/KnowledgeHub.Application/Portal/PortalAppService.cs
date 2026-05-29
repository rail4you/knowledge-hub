using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.Resources;
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
    private readonly ITenantRepository _tenantRepository;

    public PortalAppService(
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorCourse, Guid> microMajorCourseRepository,
        IRepository<MicroMajorResource, Guid> microMajorResourceRepository,
        IRepository<Courses.Course, Guid> courseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<NewsArticle, Guid> newsArticleRepository,
        ITenantRepository tenantRepository)
    {
        _microMajorRepository = microMajorRepository;
        _microMajorCourseRepository = microMajorCourseRepository;
        _microMajorResourceRepository = microMajorResourceRepository;
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _newsArticleRepository = newsArticleRepository;
        _tenantRepository = tenantRepository;
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

        // Featured courses (latest 8 published)
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var featuredCourses = courseQuery
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreationTime)
            .Take(8)
            .Select(x => new CourseBriefDto
            {
                Id = x.Id,
                Title = x.Title,
                CoverImageUrl = x.CoverImageUrl,
                TeacherName = string.Empty,
                StudentCount = 0
            })
            .ToList();

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
}
