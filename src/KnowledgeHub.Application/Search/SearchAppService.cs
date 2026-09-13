using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.Search;

[IgnoreAntiforgeryToken]
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

    /// <summary>公开检索：学生端/门户可直接调用。</summary>
    [AllowAnonymous]
    public async Task<SearchResultDto> SearchAsync(SearchQueryDto input)
    {
        var result = await _meiliSearchService.SearchAsync(input);
        
        if (_currentUser.Id.HasValue)
        {
            var filters = input.FileExtensions != null
                ? string.Join(",", input.FileExtensions)
                : null;

            var sourceType = result.Items.Any(i => i.SourceType == "video") ? "video" : "document";

            await _analyticsService.LogSearchAsync(
                _currentUser.Id.Value,
                input.Query,
                0,
                result.TotalCount,
                filters,
                sourceType);
        }
        
        return result;
    }

    /// <summary>公开混合检索。</summary>
    [AllowAnonymous]
    public async Task<SearchResultDto> HybridSearchAsync(HybridSearchQueryDto input)
    {
        var result = await _meiliSearchService.HybridSearchAsync(input);
        
        if (_currentUser.Id.HasValue)
        {
            var filters = input.FileExtensions != null
                ? string.Join(",", input.FileExtensions)
                : null;

            var sourceType = result.Items.Any(i => i.SourceType == "video") ? "video" : "document";

            await _analyticsService.LogSearchAsync(
                _currentUser.Id.Value,
                input.Query,
                1,
                result.TotalCount,
                filters,
                sourceType);
        }
        
        return result;
    }

    [Authorize(KnowledgeHubPermissions.Search.Default)]
    public async Task<IndexTaskResultDto> IndexResourceAsync(IndexDocumentDto input)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ManageIndex);
        return await _meiliSearchService.IndexDocumentAsync(input.ResourceId);
    }

    [Authorize(KnowledgeHubPermissions.Search.Default)]
    public async Task<IndexTaskResultDto> RefreshDocumentIndexAsync(Guid resourceId)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ManageIndex);
        return await _meiliSearchService.RefreshDocumentIndexAsync(resourceId);
    }

    [Authorize(KnowledgeHubPermissions.Search.Default)]
    public async Task DeleteIndexAsync(Guid resourceId)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ManageIndex);
        await _meiliSearchService.DeleteDocumentAsync(resourceId);
    }

    [Authorize(KnowledgeHubPermissions.Search.Default)]
    public async Task<List<IndexStatusDto>> GetIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20)
    {
        return await _meiliSearchService.GetAllIndexingTasksAsync(skipCount, maxResultCount);
    }

    [Authorize(KnowledgeHubPermissions.Search.Default)]
    public async Task<IndexStatusDto?> GetIndexTaskStatusAsync(long taskId)
    {
        return await _meiliSearchService.GetIndexingTaskStatusAsync(taskId);
    }

    /// <summary>浏览行为埋点：登录用户可记录，匿名静默忽略。</summary>
    [AllowAnonymous]
    public async Task LogViewAsync(LogViewDto input)
    {
        await _analyticsService.LogResourceViewAsync(input);
    }

    /// <summary>当前用户搜索历史：未登录返回空。</summary>
    [AllowAnonymous]
    public async Task<PagedResultDto<SearchHistoryDto>> GetMySearchHistoryAsync(int skipCount = 0, int maxResultCount = 20)
    {
        if (!_currentUser.Id.HasValue)
        {
            return new PagedResultDto<SearchHistoryDto>(0, new List<SearchHistoryDto>());
        }
        
        return await _analyticsService.GetUserSearchHistoryAsync(
            _currentUser.Id.Value, 
            skipCount, 
            maxResultCount);
    }

    [AllowAnonymous]
    public async Task DeleteMySearchHistoryAsync(Guid id)
    {
        if (!_currentUser.Id.HasValue) return;
        await _analyticsService.DeleteSearchHistoryAsync(_currentUser.Id.Value, id);
    }

    [AllowAnonymous]
    public async Task ClearMySearchHistoryAsync()
    {
        if (!_currentUser.Id.HasValue) return;
        await _analyticsService.ClearUserSearchHistoryAsync(_currentUser.Id.Value);
    }

    [Authorize(KnowledgeHubPermissions.Search.Default)]
    public async Task<SearchStatsDto> GetSearchStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        return await _analyticsService.GetSearchStatsAsync(startDate, endDate);
    }

    /// <summary>热门搜索词：门户/学生端展示使用，公开。</summary>
    [AllowAnonymous]
    public async Task<List<PopularSearchDto>> GetPopularSearchesAsync(int count = 10)
    {
        return await _analyticsService.GetPopularSearchesAsync(count);
    }

    /// <summary>热门资源：门户/学生端展示使用，公开。</summary>
    [AllowAnonymous]
    public async Task<List<TopResourceDto>> GetTopResourcesAsync(int count = 10)
    {
        return await _analyticsService.GetTopResourcesAsync(count);
    }
}
