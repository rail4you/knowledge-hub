using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using KnowledgeHub.EntityFrameworkCore;
using System.Data;

namespace KnowledgeHub.Application.Search;

public class SearchStatisticsAppService : KnowledgeHubAppService, ISearchStatisticsAppService
{
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;

    public SearchStatisticsAppService(
        KnowledgeHubDbContext dbContext,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
    }

    [Authorize(KnowledgeHubPermissions.Search.ViewStatistics)]
    public async Task<SearchDashboardDto> GetDashboardAsync(SearchStatsQueryDto input)
    {
        var startDate = input.StartDate ?? DateTime.Now.AddDays(-30);
        var endDate = input.EndDate ?? DateTime.Now;
        var tenantId = input.TenantId;

        if (_currentTenant.Id.HasValue)
        {
            tenantId = _currentTenant.Id;
        }

        var tenantFilter = tenantId.HasValue 
            ? $"AND (\"TenantId\" = '{tenantId}' OR \"TenantId\" IS NULL)"
            : "";

        var startDateStr = startDate.ToString("yyyy-MM-dd HH:mm:ss");
        var endDateStr = endDate.ToString("yyyy-MM-dd HH:mm:ss");

        var allStats = await GetSearchStatsAsync(startDateStr, endDateStr, null, tenantFilter);
        var documentStats = await GetSearchStatsAsync(startDateStr, endDateStr, "document", tenantFilter);
        var videoStats = await GetSearchStatsAsync(startDateStr, endDateStr, "video", tenantFilter);

        var dailyTrends = await GetDailyTrendsAsync(startDateStr, endDateStr, tenantFilter);
        var popularSearches = await GetPopularSearchesAsync(startDateStr, endDateStr, tenantFilter);
        var topResources = await GetTopResourcesAsync(startDateStr, endDateStr, tenantFilter);
        var topRated = await GetTopRatedResourcesAsync(startDateStr, endDateStr, tenantFilter);

        return new SearchDashboardDto
        {
            All = allStats,
            Document = documentStats,
            Video = videoStats,
            DailyTrends = dailyTrends,
            PopularSearches = popularSearches,
            TopResources = topResources,
            TopRatedResources = topRated
        };
    }

    private async Task<SearchStatsBreakdown> GetSearchStatsAsync(string startDate, string endDate, string? sourceType, string tenantFilter)
    {
        var sourceTypeFilter = sourceType != null ? $"AND \"SourceType\" = '{sourceType}'" : "";
        
        var todayStart = DateTime.Now.Date.ToString("yyyy-MM-dd HH:mm:ss");
        var todayEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT 
                COUNT(*) as total,
                COUNT(DISTINCT CASE WHEN ""CreationTime"" >= '{todayStart}' THEN ""UserId"" END) as today_active_users,
                COUNT(CASE WHEN ""CreationTime"" >= '{todayStart}' THEN 1 END) as today_searches,
                COUNT(DISTINCT CASE WHEN ""CreationTime"" >= '{todayStart}' AND ""UserId"" IS NOT NULL THEN ""UserId"" END) as today_active
            FROM ""KhSearchQueries"" 
            WHERE ""CreationTime"" >= '{startDate}' AND ""CreationTime"" <= '{endDate}' {tenantFilter} {sourceTypeFilter}";
        
        await _dbContext.Database.OpenConnectionAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SearchStatsBreakdown
            {
                TotalSearches = reader.GetInt64(0),
                TodaySearches = reader.GetInt32(2),
                ActiveUsers = reader.GetInt32(1),
                TodayActiveUsers = reader.GetInt32(3)
            };
        }

        return new SearchStatsBreakdown();
    }

    private async Task<List<DailySearchTrendDto>> GetDailyTrendsAsync(string startDate, string endDate, string tenantFilter)
    {
        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT 
                DATE(""CreationTime"") as date,
                COUNT(*) as total,
                COUNT(CASE WHEN ""SourceType"" = 'document' THEN 1 END) as document_count,
                COUNT(CASE WHEN ""SourceType"" = 'video' THEN 1 END) as video_count,
                COUNT(DISTINCT ""UserId"") as unique_users
            FROM ""KhSearchQueries"" 
            WHERE ""CreationTime"" >= '{startDate}' AND ""CreationTime"" <= '{endDate}' {tenantFilter}
            GROUP BY DATE(""CreationTime"")
            ORDER BY date
            LIMIT 60";

        await _dbContext.Database.OpenConnectionAsync();
        var results = new List<DailySearchTrendDto>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new DailySearchTrendDto
            {
                Date = reader.GetDateTime(0),
                TotalSearchCount = reader.GetInt32(1),
                DocumentSearchCount = reader.GetInt32(2),
                VideoSearchCount = reader.GetInt32(3),
                UniqueUsers = reader.GetInt32(4)
            });
        }
        return results;
    }

    private async Task<List<PopularSearchTermDto>> GetPopularSearchesAsync(string startDate, string endDate, string tenantFilter)
    {
        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT 
                LOWER(""QueryText"") as keyword,
                ""SourceType"",
                COUNT(*) as count
            FROM ""KhSearchQueries"" 
            WHERE ""CreationTime"" >= '{startDate}' AND ""CreationTime"" <= '{endDate}' 
                AND ""SourceType"" != 'all' {tenantFilter}
            GROUP BY LOWER(""QueryText""), ""SourceType""
            ORDER BY count DESC
            LIMIT 10";

        await _dbContext.Database.OpenConnectionAsync();
        var results = new List<PopularSearchTermDto>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new PopularSearchTermDto
            {
                Keyword = reader.GetString(0),
                SourceType = reader.GetString(1),
                Count = reader.GetInt32(2)
            });
        }
        return results;
    }

    private async Task<List<TopResourceStatsDto>> GetTopResourcesAsync(string startDate, string endDate, string tenantFilter)
    {
        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT v.""ResourceId"", r.""Name"", COUNT(*) as view_count
            FROM ""KhResourceViewLogs"" v
            LEFT JOIN ""AppResources"" r ON v.""ResourceId"" = r.""Id""
            WHERE v.""CreationTime"" >= '{startDate}' AND v.""CreationTime"" <= '{endDate}' 
                AND r.""Name"" IS NOT NULL AND r.""Name"" != '' {tenantFilter}
            GROUP BY v.""ResourceId"", r.""Name""
            ORDER BY view_count DESC
            LIMIT 10";

        await _dbContext.Database.OpenConnectionAsync();
        var results = new List<TopResourceStatsDto>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TopResourceStatsDto
            {
                ResourceId = reader.GetGuid(0),
                ResourceName = reader.GetString(1),
                SearchCount = reader.GetInt32(2),
                ClickCount = 0,
                ClickRate = 0
            });
        }
        return results;
    }

    private async Task<List<TopRatedResourceDto>> GetTopRatedResourcesAsync(string startDate, string endDate, string tenantFilter)
    {
        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT r.""ResourceId"", res.""Name"", AVG(r.""Rating"") as avg_rating, COUNT(*) as review_count
            FROM ""KhResourceReviews"" r
            LEFT JOIN ""AppResources"" res ON r.""ResourceId"" = res.""Id""
            WHERE res.""Name"" IS NOT NULL AND res.""Name"" != '' {tenantFilter}
            GROUP BY r.""ResourceId"", res.""Name""
            HAVING COUNT(*) >= 1
            ORDER BY avg_rating DESC, review_count DESC
            LIMIT 10";

        await _dbContext.Database.OpenConnectionAsync();
        var results = new List<TopRatedResourceDto>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TopRatedResourceDto
            {
                ResourceId = reader.GetGuid(0),
                ResourceName = reader.GetString(1),
                AverageRating = Math.Round(reader.GetDouble(2), 1),
                ReviewCount = reader.GetInt32(3)
            });
        }
        return results;
    }
}
