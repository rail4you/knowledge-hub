using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.Search;

public class SearchAppService : KnowledgeHubAppService, ISearchAppService
{
    private readonly IMeiliSearchService _meiliSearchService;
    private readonly ISearchAnalyticsService _analyticsService;
    private readonly ICurrentUser _currentUser;

    public SearchAppService(
        IMeiliSearchService meiliSearchService,
        ISearchAnalyticsService analyticsService,
        ICurrentUser currentUser)
    {
        _meiliSearchService = meiliSearchService;
        _analyticsService = analyticsService;
        _currentUser = currentUser;
    }

    public async Task<SearchResultDto> SearchAsync(SearchQueryDto input)
    {
        var result = await _meiliSearchService.SearchAsync(input);
        
        if (_currentUser.Id.HasValue)
        {
            var filters = input.ResourceTypes != null 
                ? string.Join(",", input.ResourceTypes) 
                : null;
            
            await _analyticsService.LogSearchAsync(
                _currentUser.Id.Value,
                input.Query,
                0,
                result.TotalCount,
                filters);
        }
        
        return result;
    }

    public async Task<SearchResultDto> HybridSearchAsync(HybridSearchQueryDto input)
    {
        var result = await _meiliSearchService.HybridSearchAsync(input);
        
        if (_currentUser.Id.HasValue)
        {
            var filters = input.ResourceTypes != null 
                ? string.Join(",", input.ResourceTypes) 
                : null;
            
            await _analyticsService.LogSearchAsync(
                _currentUser.Id.Value,
                input.Query,
                1,
                result.TotalCount,
                filters);
        }
        
        return result;
    }

    public async Task<IndexTaskResultDto> IndexResourceAsync(IndexDocumentDto input)
    {
        return await _meiliSearchService.IndexDocumentAsync(input.ResourceId);
    }

    public async Task<IndexTaskResultDto> RefreshDocumentIndexAsync(Guid resourceId)
    {
        return await _meiliSearchService.RefreshDocumentIndexAsync(resourceId);
    }

    public async Task DeleteIndexAsync(Guid resourceId)
    {
        await _meiliSearchService.DeleteDocumentAsync(resourceId);
    }

    public async Task<List<IndexStatusDto>> GetIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20)
    {
        return await _meiliSearchService.GetAllIndexingTasksAsync(skipCount, maxResultCount);
    }

    public async Task<IndexStatusDto?> GetIndexTaskStatusAsync(long taskId)
    {
        return await _meiliSearchService.GetIndexingTaskStatusAsync(taskId);
    }

    public async Task LogViewAsync(LogViewDto input)
    {
        await _analyticsService.LogResourceViewAsync(input);
    }

    public async Task<List<SearchHistoryDto>> GetMySearchHistoryAsync(int skipCount = 0, int maxResultCount = 20)
    {
        if (!_currentUser.Id.HasValue)
        {
            return new List<SearchHistoryDto>();
        }
        
        return await _analyticsService.GetUserSearchHistoryAsync(
            _currentUser.Id.Value, 
            skipCount, 
            maxResultCount);
    }

    public async Task<SearchStatsDto> GetSearchStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        return await _analyticsService.GetSearchStatsAsync(startDate, endDate);
    }

    public async Task<List<PopularSearchDto>> GetPopularSearchesAsync(int count = 10)
    {
        return await _analyticsService.GetPopularSearchesAsync(count);
    }

    public async Task<List<TopResourceDto>> GetTopResourcesAsync(int count = 10)
    {
        return await _analyticsService.GetTopResourcesAsync(count);
    }
}
