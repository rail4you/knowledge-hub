using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using KnowledgeHub.EntityFrameworkCore;

namespace KnowledgeHub.Application.Search;

public class SearchStatisticsAppService : KnowledgeHubAppService, ISearchStatisticsAppService
{
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public SearchStatisticsAppService(
        KnowledgeHubDbContext dbContext,
        ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [Authorize(KnowledgeHubPermissions.Search.ViewStatistics)]
    public async Task<SearchDashboardDto> GetDashboardAsync(SearchStatsQueryDto input)
    {
        var startDate = input.StartDate ?? DateTime.Now.AddDays(-30);
        var endDate = input.EndDate ?? DateTime.Now;
        var tenantId = input.TenantId;

        // 如果当前用户是租户用户，只能看自己租户的数据（忽略 input.TenantId）
        if (_currentTenant.Id.HasValue)
        {
            tenantId = _currentTenant.Id;
        }

        // 租户用户：只匹配该租户的数据
        // Host 管理员：指定租户时匹配该租户数据，不指定租户时匹配所有租户数据（全局汇总）
        var tenantFilter = tenantId.HasValue
            ? $"\"TenantId\" = '{tenantId}'"
            : "1=1";

        var startDateStr = startDate.ToString("yyyy-MM-dd HH:mm:ss");
        var endDateStr = endDate.ToString("yyyy-MM-dd HH:mm:ss");

        // 获取统计数据
        var allStats = await GetSearchStatsAsync(startDateStr, endDateStr, null, tenantFilter);
        var documentStats = await GetSearchStatsAsync(startDateStr, endDateStr, "document", tenantFilter);
        var videoStats = await GetSearchStatsAsync(startDateStr, endDateStr, "video", tenantFilter);

        var dailyTrends = await GetDailyTrendsAsync(startDateStr, endDateStr, tenantFilter);
        var popularSearches = await GetPopularSearchesAsync(startDateStr, endDateStr, tenantFilter);

        // 热门资源和高评分资源暂时返回空列表
        var topResources = new List<TopResourceStatsDto>();
        var topRated = new List<TopRatedResourceDto>();

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
            WHERE ""CreationTime"" >= '{startDate}' AND ""CreationTime"" <= '{endDate}'
                AND {tenantFilter} {sourceTypeFilter}";

        await _dbContext.Database.OpenConnectionAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SearchStatsBreakdown
            {
                TotalSearches = reader.GetInt64(0),
                TodaySearches = reader.GetInt64(2),
                ActiveUsers = reader.GetInt64(1),
                TodayActiveUsers = reader.GetInt64(3)
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
            WHERE ""CreationTime"" >= '{startDate}' AND ""CreationTime"" <= '{endDate}'
                AND {tenantFilter}
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
                AND ""SourceType"" != 'all'
                AND {tenantFilter}
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
}
