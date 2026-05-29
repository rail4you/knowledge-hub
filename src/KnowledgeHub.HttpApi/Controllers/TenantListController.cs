using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Controllers;

[Route("api/public")]
[AllowAnonymous]
public class TenantListController : AbpControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<KnowledgeHub.Courses.Course, Guid> _courseRepository;
    private readonly IRepository<KnowledgeHub.Resources.Resource, Guid> _resourceRepository;

    public TenantListController(
        ITenantRepository tenantRepository,
        IRepository<KnowledgeHub.Courses.Course, Guid> courseRepository,
        IRepository<KnowledgeHub.Resources.Resource, Guid> resourceRepository)
    {
        _tenantRepository = tenantRepository;
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
    }

    [HttpGet("tenants")]
    public async Task<List<TenantInfoDto>> GetTenants()
    {
        var tenants = await _tenantRepository.GetListAsync();
        
        var result = new List<TenantInfoDto>
        {
            new TenantInfoDto { Id = null, Name = "全局" }
        };
        
        result.AddRange(tenants
            .Select(t => new TenantInfoDto { Id = t.Id.ToString(), Name = t.Name }));
        
        return result;
    }

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
