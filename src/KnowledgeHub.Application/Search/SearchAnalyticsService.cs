using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Domain.Search.Enums;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.Search;

public class SearchAnalyticsService : ISearchAnalyticsService
{
    private readonly IRepository<SearchQuery, Guid> _searchQueryRepository;
    private readonly IRepository<ResourceViewLog, Guid> _viewLogRepository;
    private readonly IRepository<SearchStatistics, Guid> _statisticsRepository;
    private readonly IRepository<ResourceExposure, Guid> _exposureRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public SearchAnalyticsService(
        IRepository<SearchQuery, Guid> searchQueryRepository,
        IRepository<ResourceViewLog, Guid> viewLogRepository,
        IRepository<SearchStatistics, Guid> statisticsRepository,
        IRepository<ResourceExposure, Guid> exposureRepository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _searchQueryRepository = searchQueryRepository;
        _viewLogRepository = viewLogRepository;
        _statisticsRepository = statisticsRepository;
        _exposureRepository = exposureRepository;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task LogSearchAsync(Guid userId, string query, int searchType, int resultCount, string? filters, string sourceType = "all")
    {
        var searchQuery = new SearchQuery
        {
            UserId = userId,
            QueryText = query,
            SearchType = (SearchType)searchType,
            ResultCount = resultCount,
            Filters = filters,
            TenantId = _currentTenant.Id,
            SourceType = sourceType
        };
        
        await _searchQueryRepository.InsertAsync(searchQuery);
        
        await UpdateDailyStatisticsAsync(resultCount, query);
    }

    public async Task LogResourceViewAsync(LogViewDto input)
    {
        var viewLog = new ResourceViewLog
        {
            ResourceId = input.ResourceId,
            UserId = _currentUser.Id ?? Guid.Empty,
            PageNumber = input.PageNumber,
            ViewDurationSeconds = input.ViewDurationSeconds,
            ViewSource = (ViewSource)input.ViewSource,
            TenantId = _currentTenant.Id
        };
        
        await _viewLogRepository.InsertAsync(viewLog);
        
        var exposure = await _exposureRepository.FirstOrDefaultAsync(x => x.ResourceId == input.ResourceId);
        if (exposure == null)
        {
            exposure = new ResourceExposure
            {
                ResourceId = input.ResourceId,
                TimesInResults = 0,
                TimesClicked = 1,
                LastSeen = DateTime.UtcNow,
                TenantId = _currentTenant.Id
            };
            await _exposureRepository.InsertAsync(exposure);
        }
        else
        {
            exposure.TimesClicked++;
            exposure.LastSeen = DateTime.UtcNow;
            await _exposureRepository.UpdateAsync(exposure);
        }
    }

    public async Task<SearchStatsDto> GetSearchStatsAsync(DateTime? startDate, DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;
        
        var queries = await _searchQueryRepository.GetListAsync();
        var filteredQueries = queries
            .Where(q => q.CreationTime >= start && q.CreationTime <= end)
            .ToList();
        
        var uniqueUsers = filteredQueries.Select(q => q.UserId).Distinct().Count();
        var avgResults = filteredQueries.Any() 
            ? filteredQueries.Average(q => q.ResultCount) 
            : 0;
        
        var dailyTrends = filteredQueries
            .GroupBy(q => q.CreationTime.Date)
            .Select(g => new SearchTrendDto
            {
                Date = g.Key,
                SearchCount = g.Count()
            })
            .OrderByDescending(x => x.Date)
            .Take(30)
            .ToList();
        
        var topTerm = filteredQueries
            .GroupBy(q => q.QueryText.ToLower())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        return new SearchStatsDto
        {
            TotalSearches = filteredQueries.Count,
            UniqueUsers = uniqueUsers,
            AvgResultsPerSearch = avgResults,
            DailyTrends = dailyTrends,
            TopSearchTerm = topTerm
        };
    }

    public async Task<List<PopularSearchDto>> GetPopularSearchesAsync(int count = 10)
    {
        var queries = await _searchQueryRepository.GetListAsync();
        
        return queries
            .GroupBy(q => q.QueryText.ToLower())
            .Select(g => new PopularSearchDto
            {
                Query = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(count)
            .ToList();
    }

    public async Task<List<TopResourceDto>> GetTopResourcesAsync(int count = 10)
    {
        var exposures = await _exposureRepository.GetListAsync();
        
        return exposures
            .OrderByDescending(x => x.TimesClicked)
            .Take(count)
            .Select(x => new TopResourceDto
            {
                ResourceId = x.ResourceId,
                ResourceName = "",
                ExposureCount = x.TimesInResults,
                ClickCount = x.TimesClicked,
                ClickRate = x.TimesInResults > 0 
                    ? (double)x.TimesClicked / x.TimesInResults 
                    : 0
            })
            .ToList();
    }

    public async Task<List<SearchHistoryDto>> GetUserSearchHistoryAsync(Guid userId, int skipCount = 0, int maxResultCount = 20)
    {
        var queries = await _searchQueryRepository.GetListAsync();
        
        return queries
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(q => new SearchHistoryDto
            {
                Id = q.Id,
                QueryText = q.QueryText,
                CreationTime = q.CreationTime,
                ResultCount = q.ResultCount
            })
            .ToList();
    }

    private async Task UpdateDailyStatisticsAsync(int resultCount, string query)
    {
        var today = DateTime.UtcNow.Date;
        var tenantId = _currentTenant.Id;
        
        var stats = await _statisticsRepository.FirstOrDefaultAsync(
            x => x.Date == today && x.TenantId == tenantId);
        
        if (stats == null)
        {
            stats = new SearchStatistics
            {
                Date = today,
                TotalSearches = 1,
                UniqueUsers = 1,
                AvgResultCount = resultCount,
                TopSearchTerm = query,
                TenantId = tenantId
            };
            await _statisticsRepository.InsertAsync(stats);
        }
        else
        {
            stats.TotalSearches++;
            stats.AvgResultCount = (stats.AvgResultCount * (stats.TotalSearches - 1) + resultCount) / stats.TotalSearches;
            
            var currentTop = stats.TopSearchTerm?.ToLower() ?? "";
            var queryLower = query.ToLower();
            if (!currentTop.Contains(queryLower))
            {
                stats.TopSearchTerm = query;
            }
            
            await _statisticsRepository.UpdateAsync(stats);
        }
    }
}
