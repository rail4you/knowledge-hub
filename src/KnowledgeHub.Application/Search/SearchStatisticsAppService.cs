using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Application.Search;

public class SearchStatisticsAppService : KnowledgeHubAppService, ISearchStatisticsAppService
{
    private readonly IRepository<SearchQuery, Guid> _searchQueryRepository;
    private readonly IRepository<ResourceViewLog, Guid> _viewLogRepository;
    private readonly IRepository<ResourceReview, Guid> _reviewRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;

    public SearchStatisticsAppService(
        IRepository<SearchQuery, Guid> searchQueryRepository,
        IRepository<ResourceViewLog, Guid> viewLogRepository,
        IRepository<ResourceReview, Guid> reviewRepository,
        IRepository<Resource, Guid> resourceRepository,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository)
    {
        _searchQueryRepository = searchQueryRepository;
        _viewLogRepository = viewLogRepository;
        _reviewRepository = reviewRepository;
        _resourceRepository = resourceRepository;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
    }

    [Authorize(KnowledgeHubPermissions.Search.ViewStatistics)]
    public async Task<SearchDashboardDto> GetDashboardAsync(SearchStatsQueryDto input)
    {
        var startDate = input.StartDate ?? DateTime.UtcNow.AddDays(-30);
        var endDate = input.EndDate ?? DateTime.UtcNow;
        var tenantId = input.TenantId;

        // If not host user, force current tenant
        if (_currentTenant.Id.HasValue)
        {
            tenantId = _currentTenant.Id;
        }

        var queries = await _searchQueryRepository.GetListAsync();
        var filtered = queries.AsQueryable();

        if (tenantId.HasValue)
        {
            filtered = filtered.Where(q => q.TenantId == tenantId || q.TenantId == null);
        }

        filtered = filtered.Where(q => q.CreationTime >= startDate && q.CreationTime <= endDate);

        var queryList = filtered.ToList();
        var today = DateTime.UtcNow.Date;
        var todayQueries = queryList.Where(q => q.CreationTime.Date == today).ToList();

        // Daily trends
        var dailyTrends = queryList
            .GroupBy(q => q.CreationTime.Date)
            .Select(g => new DailySearchTrendDto
            {
                Date = g.Key,
                SearchCount = g.Count(),
                UniqueUsers = g.Select(q => q.UserId).Distinct().Count()
            })
            .OrderBy(x => x.Date)
            .Take(60)
            .ToList();

        // Popular searches
        var popularSearches = queryList
            .GroupBy(q => q.QueryText.ToLower())
            .Select(g => new PopularSearchTermDto
            {
                Keyword = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToList();

        // Top resources by view count
        var resources = await _resourceRepository.GetListAsync();
        var resourceDict = resources.ToDictionary(r => r.Id, r => r.Name);

        var viewLogs = await _viewLogRepository.GetListAsync();
        var viewLogQuery = viewLogs.AsQueryable();
        if (tenantId.HasValue)
        {
            viewLogQuery = viewLogQuery.Where(v => v.TenantId == tenantId || v.TenantId == null);
        }

        var viewCountByResource = viewLogQuery
            .GroupBy(v => v.ResourceId)
            .ToDictionary(g => g.Key, g => g.Count());

        var topResources = viewCountByResource
            .OrderByDescending(x => x.Value)
            .Take(20)
            .Select(x => new TopResourceStatsDto
            {
                ResourceId = x.Key,
                ResourceName = resourceDict.GetValueOrDefault(x.Key, ""),
                SearchCount = x.Value,
                ClickCount = 0
            })
            .ToList();

        // Top rated resources
        var reviews = await _reviewRepository.GetListAsync();
        var reviewQuery = reviews.AsQueryable();

        var topRated = reviewQuery
            .GroupBy(r => r.ResourceId)
            .Select(g => new TopRatedResourceDto
            {
                ResourceId = g.Key,
                ResourceName = resourceDict.GetValueOrDefault(g.Key, ""),
                AverageRating = Math.Round(g.Average(r => r.Rating), 1),
                ReviewCount = g.Count()
            })
            .Where(x => x.ReviewCount >= 1)
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.ReviewCount)
            .Take(20)
            .ToList();

        return new SearchDashboardDto
        {
            TotalSearches = queryList.Count,
            TodaySearches = todayQueries.Count,
            ActiveUsers = queryList.Select(q => q.UserId).Distinct().Count(),
            TodayActiveUsers = todayQueries.Select(q => q.UserId).Distinct().Count(),
            DailyTrends = dailyTrends,
            PopularSearches = popularSearches,
            TopResources = topResources,
            TopRatedResources = topRated
        };
    }
}
