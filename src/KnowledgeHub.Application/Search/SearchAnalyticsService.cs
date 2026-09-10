using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Domain.Search.Enums;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
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
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IMeiliSearchService _meiliSearchService;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ILogger<SearchAnalyticsService> _logger;
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly IDataFilter<IMultiTenant> _dataFilter;

    public SearchAnalyticsService(
        IRepository<SearchQuery, Guid> searchQueryRepository,
        IRepository<ResourceViewLog, Guid> viewLogRepository,
        IRepository<SearchStatistics, Guid> statisticsRepository,
        IRepository<ResourceExposure, Guid> exposureRepository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IMeiliSearchService meiliSearchService,
        IRepository<Resource, Guid> resourceRepository,
        ILogger<SearchAnalyticsService> logger,
        KnowledgeHubDbContext dbContext,
        IDataFilter<IMultiTenant> dataFilter)
    {
        _searchQueryRepository = searchQueryRepository;
        _viewLogRepository = viewLogRepository;
        _statisticsRepository = statisticsRepository;
        _exposureRepository = exposureRepository;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _meiliSearchService = meiliSearchService;
        _resourceRepository = resourceRepository;
        _logger = logger;
        _dbContext = dbContext;
        _dataFilter = dataFilter;
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
        // 搜索索引可能残留已删除资源的文档（删除时 Meili 不可用、DB 重建等）。
        // KhResourceViewLogs / KhResourceExposures 对 AppResources 有外键，
        // 直接写入会 FK 冲突 500。资源已不存在时跳过（仅记一条警告）。
        Resource? resource;
        using (_dataFilter.Disable())
        {
            resource = await _resourceRepository.FindAsync(input.ResourceId);
        }
        if (resource == null)
        {
            _logger.LogWarning("Skipping view log for missing resource {ResourceId}", input.ResourceId);
            return;
        }

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
        // 热门词只面向「当前租户内的学生」产生的搜索记录：
        // 1) 无租户上下文（如 Host 场景）时直接返回空；
        // 2) 只统计当前租户内拥有 Student 角色（含 host 级 Student 角色）的用户。
        var tenantId = _currentTenant.Id;
        if (tenantId == null)
        {
            return new List<PopularSearchDto>();
        }

        var studentUserIds = await GetTenantStudentUserIdsAsync(tenantId.Value);
        if (studentUserIds.Count == 0)
        {
            return new List<PopularSearchDto>();
        }

        var queryable = await _searchQueryRepository.GetQueryableAsync();

        var popular = await _asyncExecuter.ToListAsync(
            queryable
                .Where(q => q.TenantId == tenantId && studentUserIds.Contains(q.UserId))
                .GroupBy(q => q.QueryText.ToLower())
                .Select(g => new PopularSearchDto
                {
                    Query = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(count * 3)); // 多取一些，过滤后仍可能凑够 count

        if (popular.Count == 0) return popular;

        var result = new List<PopularSearchDto>();
        foreach (var item in popular)
        {
            try
            {
                var searchResult = await _meiliSearchService.SearchAsync(new SearchQueryDto
                {
                    Query = item.Query,
                    MaxResultCount = 1,
                    SkipCount = 0
                });

                if (searchResult.TotalCount > 0)
                {
                    result.Add(item);
                    if (result.Count >= count) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate popular search term '{Query}' against MeiliSearch", item.Query);
                // 验证失败的词跳过，不影响其它词
            }
        }

        return result;
    }

    /// <summary>
    /// 收集当前租户内拥有 Student 角色（含 host 级 Student 角色）的用户 ID。
    /// 与 RecruitmentLiveAppService.GetTenantStudentsAsync 保持一致：
    /// AbpRoles / AbpUserRoles 受多租户过滤，而租户用户可能分配的是 host 级
    /// Student 角色（AbpUserRoles.TenantId = NULL），因此需要临时禁用多租户过滤。
    /// </summary>
    private async Task<List<Guid>> GetTenantStudentUserIdsAsync(Guid tenantId)
    {
        const string studentRoleName = "Student";

        using (_dataFilter.Disable())
        {
            // 租户自己的 Student 角色 + host 级 Student 角色（可被租户用户共享）
            var studentRoleIds = await _dbContext.Set<IdentityRole>()
                .Where(r => r.Name == studentRoleName && r.TenantId == tenantId)
                .Select(r => r.Id)
                .ToListAsync();

            var hostStudentRoleIds = await _dbContext.Set<IdentityRole>()
                .Where(r => r.Name == studentRoleName && r.TenantId == null)
                .Select(r => r.Id)
                .ToListAsync();
            studentRoleIds.AddRange(hostStudentRoleIds);

            if (studentRoleIds.Count == 0)
            {
                return new List<Guid>();
            }

            return await _dbContext.Set<IdentityUserRole>()
                .Where(ur => studentRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();
        }
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

    public async Task<PagedResultDto<SearchHistoryDto>> GetUserSearchHistoryAsync(Guid userId, int skipCount = 0, int maxResultCount = 20)
    {
        var queries = await _searchQueryRepository.GetListAsync();

        var userQueries = queries
            .Where(q => q.UserId == userId)
            .GroupBy(q => q.QueryText.Trim().ToLower())
            .Select(g => g.OrderByDescending(q => q.CreationTime).First())
            .OrderByDescending(q => q.CreationTime)
            .ToList();

        var totalCount = userQueries.Count;

        var items = userQueries
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

        return new PagedResultDto<SearchHistoryDto>(totalCount, items);
    }

    public async Task DeleteSearchHistoryAsync(Guid userId, Guid id)
    {
        var item = await _searchQueryRepository.FirstOrDefaultAsync(q => q.Id == id && q.UserId == userId);
        if (item == null) return;

        var normalized = item.QueryText.Trim().ToLower();
        var duplicates = await _searchQueryRepository.GetListAsync(
            q => q.UserId == userId && q.QueryText.Trim().ToLower() == normalized);
        if (duplicates.Any())
        {
            await _searchQueryRepository.DeleteManyAsync(duplicates);
        }
    }

    public async Task ClearUserSearchHistoryAsync(Guid userId)
    {
        var items = await _searchQueryRepository.GetListAsync(q => q.UserId == userId);
        if (items.Any())
        {
            await _searchQueryRepository.DeleteManyAsync(items);
        }
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
