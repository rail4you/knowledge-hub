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
using KnowledgeHub.EntityFrameworkCore;

namespace KnowledgeHub.Application.Search;

public class ResourceRecommendationAppService : KnowledgeHubAppService, IResourceRecommendationAppService
{
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public ResourceRecommendationAppService(
        KnowledgeHubDbContext dbContext,
        ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    private string GetTenantFilter()
    {
        var tenantId = _currentTenant.Id;
        if (tenantId.HasValue)
        {
            return $"AND r.\"TenantId\" = '{tenantId}'";
        }
        return "";
    }

    private string GetStatusFilter()
    {
        return "AND r.\"Status\" IN (2, 3)";
    }

    [Authorize(KnowledgeHubPermissions.Resources.ViewRecommendation)]
    public async Task<List<RecommendedResourceDto>> GetTrendingResourcesAsync(int count = 10)
    {
        var tenantFilter = GetTenantFilter();
        var statusFilter = GetStatusFilter();

        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT
                    r.""Id"", r.""Name"", r.""Description"", r.""ResourceType"",
                    r.""CategoryId"", rc.""Name"" as CategoryName, r.""Keywords"",
                    r.""FileExtension"", r.""FileSize"",
                    r.""ViewCount"", r.""CollectionCount"", r.""DownloadCount"",
                    COALESCE(rv.avg_rating, 0) as AvgRating,
                    COALESCE(rv.review_count, 0) as TotalReviews,
                    r.""CreationTime"",
                    -- Trending score: time-decayed views + weighted collection/download + rating
                    (
                        COALESCE(v_recent.view_score, 0) +
                        r.""CollectionCount"" * 5 +
                        r.""DownloadCount"" * 3 +
                        COALESCE(rv.avg_rating, 0) * 2
                    ) as Score
                FROM ""AppResources"" r
                LEFT JOIN ""AppResourceCategories"" rc ON r.""CategoryId"" = rc.""Id""
                LEFT JOIN (
                    SELECT ""ResourceId"", AVG(""Rating"")::numeric as avg_rating, COUNT(*)::int as review_count
                    FROM ""KhResourceReviews""
                    GROUP BY ""ResourceId""
                ) rv ON r.""Id"" = rv.""ResourceId""
                LEFT JOIN (
                    SELECT
                        ""ResourceId"",
                        SUM(
                            CASE
                                WHEN ""CreationTime"" >= NOW() - INTERVAL '7 days' THEN 3
                                WHEN ""CreationTime"" >= NOW() - INTERVAL '14 days' THEN 2
                                WHEN ""CreationTime"" >= NOW() - INTERVAL '30 days' THEN 1
                                ELSE 0
                            END
                        ) as view_score
                    FROM ""KhResourceViewLogs""
                    WHERE ""CreationTime"" >= NOW() - INTERVAL '30 days'
                    GROUP BY ""ResourceId""
                ) v_recent ON r.""Id"" = v_recent.""ResourceId""
                WHERE r.""IsDeleted"" = false {statusFilter} {tenantFilter}
                ORDER BY Score DESC
                LIMIT {count}";

            var results = new List<RecommendedResourceDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapReaderToRecommended(reader, "同类热门"));
            }
            return results;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Authorize(KnowledgeHubPermissions.Resources.ViewRecommendation)]
    public async Task<List<RecommendedResourceDto>> GetCategoryBasedRecommendationsAsync(int count = 6)
    {
        var userId = CurrentUser.Id;
        var tenantFilter = GetTenantFilter();
        var statusFilter = GetStatusFilter();

        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                WITH user_categories AS (
                    SELECT r.""CategoryId"", COUNT(*) as interaction_count
                    FROM ""KhResourceViewLogs"" vl
                    JOIN ""AppResources"" r ON vl.""ResourceId"" = r.""Id""
                    WHERE vl.""UserId"" = '{userId}'
                        AND r.""CategoryId"" IS NOT NULL
                    GROUP BY r.""CategoryId""
                    ORDER BY interaction_count DESC
                    LIMIT 3
                ),
                user_viewed AS (
                    SELECT DISTINCT ""ResourceId"" FROM ""KhResourceViewLogs"" WHERE ""UserId"" = '{userId}'
                )
                SELECT
                    r.""Id"", r.""Name"", r.""Description"", r.""ResourceType"",
                    r.""CategoryId"", rc.""Name"" as CategoryName, r.""Keywords"",
                    r.""FileExtension"", r.""FileSize"",
                    r.""ViewCount"", r.""CollectionCount"", r.""DownloadCount"",
                    COALESCE(rv.avg_rating, 0) as AvgRating,
                    COALESCE(rv.review_count, 0) as TotalReviews,
                    r.""CreationTime"",
                    (
                        r.""ViewCount"" +
                        r.""CollectionCount"" * 5 +
                        r.""DownloadCount"" * 3
                    ) as Score
                FROM ""AppResources"" r
                LEFT JOIN ""AppResourceCategories"" rc ON r.""CategoryId"" = rc.""Id""
                LEFT JOIN (
                    SELECT ""ResourceId"", AVG(""Rating"")::numeric as avg_rating, COUNT(*)::int as review_count
                    FROM ""KhResourceReviews""
                    GROUP BY ""ResourceId""
                ) rv ON r.""Id"" = rv.""ResourceId""
                WHERE r.""IsDeleted"" = false {statusFilter} {tenantFilter}
                    AND r.""CategoryId"" IN (SELECT ""CategoryId"" FROM user_categories)
                    AND r.""Id"" NOT IN (SELECT ""ResourceId"" FROM user_viewed)
                ORDER BY Score DESC
                LIMIT {count}";

            var results = new List<RecommendedResourceDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapReaderToRecommended(reader, "基于你的兴趣"));
            }

            // Fallback to trending if not enough results
            if (results.Count < count)
            {
                var trending = await GetTrendingResourcesAsync(count - results.Count);
                var existingIds = results.Select(r => r.ResourceId).ToHashSet();
                results.AddRange(trending.Where(t => !existingIds.Contains(t.ResourceId)));
            }

            return results;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Authorize(KnowledgeHubPermissions.Resources.ViewRecommendation)]
    public async Task<List<RecommendedResourceDto>> GetPersonalizedRecommendationsAsync(int count = 10)
    {
        var userId = CurrentUser.Id;
        var tenantFilter = GetTenantFilter();
        var statusFilter = GetStatusFilter();

        // Check if user has enough history for personalized recs
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $@"
                SELECT COUNT(DISTINCT ""ResourceId"")
                FROM ""KhResourceViewLogs""
                WHERE ""UserId"" = '{userId}'";
            var viewCount = 0;
            using (var checkReader = await checkCmd.ExecuteReaderAsync())
            {
                if (await checkReader.ReadAsync())
                {
                    viewCount = checkReader.GetInt32(0);
                }
            }

            // Cold start: fallback to trending
            if (viewCount < 3)
            {
                // Close connection before calling GetTrendingResourcesAsync which opens its own
                await connection.CloseAsync();
                var trending = await GetTrendingResourcesAsync(count);
                foreach (var t in trending)
                {
                    t.RecommendationReason = "热门推荐";
                }
                return trending;
            }

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                WITH user_viewed AS (
                    SELECT DISTINCT ""ResourceId"" FROM ""KhResourceViewLogs"" WHERE ""UserId"" = '{userId}'
                ),
                user_collected AS (
                    SELECT DISTINCT ""ResourceId"" FROM ""AppResourceCollections"" WHERE ""UserId"" = '{userId}'
                ),
                -- Co-view: users who viewed the same resources also viewed
                collaborative AS (
                    SELECT vl2.""ResourceId"", COUNT(DISTINCT vl2.""UserId"") as co_view_count
                    FROM ""KhResourceViewLogs"" vl1
                    JOIN ""KhResourceViewLogs"" vl2 ON vl1.""UserId"" = vl2.""UserId""
                        AND vl1.""ResourceId"" != vl2.""ResourceId""
                    WHERE vl1.""ResourceId"" IN (SELECT ""ResourceId"" FROM user_viewed)
                        AND vl2.""ResourceId"" NOT IN (SELECT ""ResourceId"" FROM user_viewed)
                    GROUP BY vl2.""ResourceId""
                ),
                -- User preferred categories
                user_cats AS (
                    SELECT r.""CategoryId""
                    FROM ""KhResourceViewLogs"" vl
                    JOIN ""AppResources"" r ON vl.""ResourceId"" = r.""Id""
                    WHERE vl.""UserId"" = '{userId}' AND r.""CategoryId"" IS NOT NULL
                    GROUP BY r.""CategoryId""
                ),
                -- User preferred resource types
                user_types AS (
                    SELECT r.""ResourceType""
                    FROM ""KhResourceViewLogs"" vl
                    JOIN ""AppResources"" r ON vl.""ResourceId"" = r.""Id""
                    WHERE vl.""UserId"" = '{userId}'
                    GROUP BY r.""ResourceType""
                ),
                all_candidates AS (
                    SELECT
                        r.""Id"",
                        -- Collaborative score (normalized)
                        COALESCE(collab.co_view_count, 0)::numeric as collab_score,
                        -- Content match: category + type
                        (CASE WHEN r.""CategoryId"" IN (SELECT ""CategoryId"" FROM user_cats) THEN 1 ELSE 0 END +
                         CASE WHEN r.""ResourceType"" IN (SELECT ""ResourceType"" FROM user_types) THEN 0.5 ELSE 0 END
                        ) as content_score,
                        -- Popularity
                        (r.""ViewCount"" + r.""CollectionCount"" * 5 + r.""DownloadCount"" * 3) as popularity_raw,
                        -- Freshness (newer = higher, max 1.0)
                        LEAST(EXTRACT(DAY FROM NOW() - r.""CreationTime"") / 90.0, 1.0) as freshness_raw,
                        -- Rating
                        COALESCE(rv.avg_rating, 0) as rating_raw
                    FROM ""AppResources"" r
                    LEFT JOIN collaborative collab ON r.""Id"" = collab.""ResourceId""
                    LEFT JOIN (
                        SELECT ""ResourceId"", AVG(""Rating"")::numeric as avg_rating
                        FROM ""KhResourceReviews""
                        GROUP BY ""ResourceId""
                    ) rv ON r.""Id"" = rv.""ResourceId""
                    WHERE r.""IsDeleted"" = false {statusFilter} {tenantFilter}
                        AND r.""Id"" NOT IN (SELECT ""ResourceId"" FROM user_viewed)
                        AND r.""Id"" NOT IN (SELECT ""ResourceId"" FROM user_collected)
                ),
                scored AS (
                    SELECT
                        ac.*,
                        -- Normalize popularity (0-1 scale relative to max)
                        ac.popularity_raw::numeric / NULLIF((SELECT MAX(popularity_raw) FROM all_candidates), 0) as popularity_score,
                        -- Invert freshness (lower days = higher score)
                        1.0 - ac.freshness_raw as freshness_score,
                        ac.rating_raw / 5.0 as rating_score
                    FROM all_candidates ac
                )
                SELECT
                    r.""Id"", r.""Name"", r.""Description"", r.""ResourceType"",
                    r.""CategoryId"", rc.""Name"" as CategoryName, r.""Keywords"",
                    r.""FileExtension"", r.""FileSize"",
                    r.""ViewCount"", r.""CollectionCount"", r.""DownloadCount"",
                    COALESCE(rv.avg_rating, 0) as AvgRating,
                    COALESCE(rv.review_count, 0) as TotalReviews,
                    r.""CreationTime"",
                    COALESCE((
                        0.35 * s.collab_score / NULLIF((SELECT MAX(collab_score) FROM scored WHERE collab_score > 0), 0) +
                        0.25 * COALESCE(s.content_score, 0) +
                        0.20 * COALESCE(s.popularity_score, 0) +
                        0.15 * COALESCE(s.freshness_score, 0) +
                        0.05 * COALESCE(s.rating_score, 0)
                    ), 0) as Score
                FROM scored s
                JOIN ""AppResources"" r ON s.""Id"" = r.""Id""
                LEFT JOIN ""AppResourceCategories"" rc ON r.""CategoryId"" = rc.""Id""
                LEFT JOIN (
                    SELECT ""ResourceId"", AVG(""Rating"")::numeric as avg_rating, COUNT(*)::int as review_count
                    FROM ""KhResourceReviews""
                    GROUP BY ""ResourceId""
                ) rv ON r.""Id"" = rv.""ResourceId""
                ORDER BY Score DESC
                LIMIT {count}";

            var results = new List<RecommendedResourceDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapReaderToRecommended(reader, "猜你喜欢"));
            }

            // Close connection before fallback to trending
            await connection.CloseAsync();

            // Fallback if not enough
            if (results.Count < count)
            {
                var trending = await GetTrendingResourcesAsync(count - results.Count);
                var existingIds = results.Select(r => r.ResourceId).ToHashSet();
                results.AddRange(trending.Where(t => !existingIds.Contains(t.ResourceId)));
            }

            return results;
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Resources.ViewRecommendation)]
    public async Task<List<RecommendedResourceDto>> GetRelatedResourcesAsync(Guid resourceId, int count = 6)
    {
        var tenantFilter = GetTenantFilter();
        var statusFilter = GetStatusFilter();

        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                WITH resource_info AS (
                    SELECT ""CategoryId"", ""Keywords"", ""ResourceType""
                    FROM ""AppResources""
                    WHERE ""Id"" = '{resourceId}'
                ),
                co_viewed AS (
                    SELECT vl2.""ResourceId"", COUNT(DISTINCT vl1.""UserId"") as co_view_count
                    FROM ""KhResourceViewLogs"" vl1
                    JOIN ""KhResourceViewLogs"" vl2 ON vl1.""UserId"" = vl2.""UserId""
                    WHERE vl1.""ResourceId"" = '{resourceId}'
                        AND vl2.""ResourceId"" != '{resourceId}'
                    GROUP BY vl2.""ResourceId""
                ),
                scored AS (
                    SELECT
                        r.""Id"",
                        -- Same category bonus
                        CASE WHEN r.""CategoryId"" = (SELECT ""CategoryId"" FROM resource_info) THEN 1.0 ELSE 0 END as category_score,
                        -- Co-viewed bonus (normalized)
                        COALESCE(cv.co_view_count, 0) as coview_raw,
                        -- Keyword overlap
                        (
                            SELECT COUNT(*) FROM (
                                SELECT unnest(string_to_array(r.""Keywords"", ',')) INTERSECT
                                SELECT unnest(string_to_array((SELECT ""Keywords"" FROM resource_info), ','))
                            ) overlap
                        )::numeric as keyword_overlap,
                        -- Rating similarity
                        ABS(COALESCE(rv.avg_rating, 0) - COALESCE(rv_self.avg_rating, 0)) as rating_diff
                    FROM ""AppResources"" r
                    LEFT JOIN co_viewed cv ON r.""Id"" = cv.""ResourceId""
                    LEFT JOIN (
                        SELECT ""ResourceId"", AVG(""Rating"")::numeric as avg_rating
                        FROM ""KhResourceReviews""
                        GROUP BY ""ResourceId""
                    ) rv ON r.""Id"" = rv.""ResourceId""
                    CROSS JOIN (
                        SELECT AVG(""Rating"")::numeric as avg_rating
                        FROM ""KhResourceReviews""
                        WHERE ""ResourceId"" = '{resourceId}'
                    ) rv_self
                    WHERE r.""IsDeleted"" = false {statusFilter} {tenantFilter}
                        AND r.""Id"" != '{resourceId}'
                )
                SELECT
                    r.""Id"", r.""Name"", r.""Description"", r.""ResourceType"",
                    r.""CategoryId"", rc.""Name"" as CategoryName, r.""Keywords"",
                    r.""FileExtension"", r.""FileSize"",
                    r.""ViewCount"", r.""CollectionCount"", r.""DownloadCount"",
                    COALESCE(rv.avg_rating, 0) as AvgRating,
                    COALESCE(rv.review_count, 0) as TotalReviews,
                    r.""CreationTime"",
                    COALESCE((
                        0.30 * s.category_score +
                        0.30 * (s.coview_raw::numeric / NULLIF((SELECT MAX(coview_raw) FROM scored WHERE coview_raw > 0), 0)) +
                        0.20 * (s.keyword_overlap / NULLIF((SELECT MAX(keyword_overlap) FROM scored WHERE keyword_overlap > 0), 0)) +
                        0.20 * (1.0 - LEAST(s.rating_diff / 5.0, 1.0))
                    ), 0) as Score
                FROM scored s
                JOIN ""AppResources"" r ON s.""Id"" = r.""Id""
                LEFT JOIN ""AppResourceCategories"" rc ON r.""CategoryId"" = rc.""Id""
                LEFT JOIN (
                    SELECT ""ResourceId"", AVG(""Rating"")::numeric as avg_rating, COUNT(*)::int as review_count
                    FROM ""KhResourceReviews""
                    GROUP BY ""ResourceId""
                ) rv ON r.""Id"" = rv.""ResourceId""
                ORDER BY Score DESC
                LIMIT {count}";

            var results = new List<RecommendedResourceDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var reason = "相关资源";
                var categoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName"));
                if (!string.IsNullOrEmpty(categoryName))
                {
                    reason = "同类资源";
                }
                results.Add(MapReaderToRecommended(reader, reason));
            }
            return results;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Authorize(KnowledgeHubPermissions.Resources.ViewStatistics)]
    public async Task<ResourceStatisticsDto> GetResourceStatisticsAsync(Guid resourceId)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT
                    -- View stats
                    COALESCE(v.total_views, 0) as TotalViews,
                    COALESCE(v.unique_viewers, 0) as UniqueViewers,
                    COALESCE(v.avg_duration, 0) as AvgViewDurationSeconds,
                    -- Download & Collection stats
                    r.""DownloadCount"" as TotalDownloads,
                    r.""CollectionCount"" as TotalCollections,
                    -- Rates
                    CASE WHEN r.""ViewCount"" > 0
                        THEN (r.""CollectionCount""::numeric / r.""ViewCount"") * 100
                        ELSE 0 END as CollectionRate,
                    CASE WHEN r.""ViewCount"" > 0
                        THEN (r.""DownloadCount""::numeric / r.""ViewCount"") * 100
                        ELSE 0 END as DownloadRate,
                    -- Rating stats
                    COALESCE(rv.avg_rating, 0) as AverageRating,
                    COALESCE(rv.total_reviews, 0) as TotalReviews,
                    COALESCE(rv.r1, 0) as R1,
                    COALESCE(rv.r2, 0) as R2,
                    COALESCE(rv.r3, 0) as R3,
                    COALESCE(rv.r4, 0) as R4,
                    COALESCE(rv.r5, 0) as R5,
                    -- View trend (last 30 days vs previous 30 days)
                    COALESCE(v_last.views_30d, 0) as ViewsLast30Days,
                    COALESCE(v_prev.views_prev_30d, 0) as ViewsPrevious30Days,
                    -- Search exposure
                    COALESCE(ex.""TimesInResults"", 0) as TimesInSearchResults,
                    COALESCE(ex.""TimesClicked"", 0) as TimesClickedFromSearch
                FROM ""AppResources"" r
                LEFT JOIN (
                    SELECT
                        ""ResourceId"",
                        COUNT(*)::int as total_views,
                        COUNT(DISTINCT ""UserId"")::int as unique_viewers,
                        COALESCE(AVG(""ViewDurationSeconds""), 0) as avg_duration
                    FROM ""KhResourceViewLogs""
                    WHERE ""ResourceId"" = '{resourceId}'
                    GROUP BY ""ResourceId""
                ) v ON r.""Id"" = v.""ResourceId""
                LEFT JOIN (
                    SELECT
                        ""ResourceId"",
                        AVG(""Rating"")::numeric as avg_rating,
                        COUNT(*)::int as total_reviews,
                        COUNT(CASE WHEN ""Rating"" = 1 THEN 1 END)::int as r1,
                        COUNT(CASE WHEN ""Rating"" = 2 THEN 1 END)::int as r2,
                        COUNT(CASE WHEN ""Rating"" = 3 THEN 1 END)::int as r3,
                        COUNT(CASE WHEN ""Rating"" = 4 THEN 1 END)::int as r4,
                        COUNT(CASE WHEN ""Rating"" = 5 THEN 1 END)::int as r5
                    FROM ""KhResourceReviews""
                    WHERE ""ResourceId"" = '{resourceId}'
                    GROUP BY ""ResourceId""
                ) rv ON r.""Id"" = rv.""ResourceId""
                LEFT JOIN (
                    SELECT
                        ""ResourceId"",
                        COUNT(*)::int as views_30d
                    FROM ""KhResourceViewLogs""
                    WHERE ""ResourceId"" = '{resourceId}'
                        AND ""CreationTime"" >= NOW() - INTERVAL '30 days'
                    GROUP BY ""ResourceId""
                ) v_last ON r.""Id"" = v_last.""ResourceId""
                LEFT JOIN (
                    SELECT
                        ""ResourceId"",
                        COUNT(*)::int as views_prev_30d
                    FROM ""KhResourceViewLogs""
                    WHERE ""ResourceId"" = '{resourceId}'
                        AND ""CreationTime"" >= NOW() - INTERVAL '60 days'
                        AND ""CreationTime"" < NOW() - INTERVAL '30 days'
                    GROUP BY ""ResourceId""
                ) v_prev ON r.""Id"" = v_prev.""ResourceId""
                LEFT JOIN (
                    SELECT
                        ""ResourceId"",
                        ""TimesInResults"",
                        ""TimesClicked""
                    FROM ""KhResourceExposures""
                    WHERE ""ResourceId"" = '{resourceId}'
                ) ex ON r.""Id"" = ex.""ResourceId""
                WHERE r.""Id"" = '{resourceId}'";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var viewsLast30 = reader.GetInt32(reader.GetOrdinal("ViewsLast30Days"));
                var viewsPrev30 = reader.GetInt32(reader.GetOrdinal("ViewsPrevious30Days"));
                var trendPct = viewsPrev30 > 0
                    ? Math.Round(((double)(viewsLast30 - viewsPrev30) / viewsPrev30) * 100, 1)
                    : (viewsLast30 > 0 ? 100.0 : 0.0);

                var timesInResults = reader.GetInt32(reader.GetOrdinal("TimesInSearchResults"));
                var timesClicked = reader.GetInt32(reader.GetOrdinal("TimesClickedFromSearch"));
                var ctr = timesInResults > 0
                    ? Math.Round(((double)timesClicked / timesInResults) * 100, 1)
                    : 0.0;

                return new ResourceStatisticsDto
                {
                    ResourceId = resourceId,
                    TotalViews = reader.GetInt32(reader.GetOrdinal("TotalViews")),
                    UniqueViewers = reader.GetInt32(reader.GetOrdinal("UniqueViewers")),
                    AvgViewDurationSeconds = Math.Round(reader.GetDouble(reader.GetOrdinal("AvgViewDurationSeconds")), 1),
                    TotalDownloads = reader.GetInt32(reader.GetOrdinal("TotalDownloads")),
                    TotalCollections = reader.GetInt32(reader.GetOrdinal("TotalCollections")),
                    CollectionRate = Math.Round(reader.GetDouble(reader.GetOrdinal("CollectionRate")), 1),
                    DownloadRate = Math.Round(reader.GetDouble(reader.GetOrdinal("DownloadRate")), 1),
                    AverageRating = Math.Round(reader.GetDouble(reader.GetOrdinal("AverageRating")), 1),
                    TotalReviews = reader.GetInt32(reader.GetOrdinal("TotalReviews")),
                    RatingDistribution = new[]
                    {
                        reader.GetInt32(reader.GetOrdinal("R1")),
                        reader.GetInt32(reader.GetOrdinal("R2")),
                        reader.GetInt32(reader.GetOrdinal("R3")),
                        reader.GetInt32(reader.GetOrdinal("R4")),
                        reader.GetInt32(reader.GetOrdinal("R5"))
                    },
                    ViewsLast30Days = viewsLast30,
                    ViewsPrevious30Days = viewsPrev30,
                    ViewTrendPercentage = trendPct,
                    TimesInSearchResults = timesInResults,
                    TimesClickedFromSearch = timesClicked,
                    ClickThroughRate = ctr
                };
            }

            return new ResourceStatisticsDto { ResourceId = resourceId };
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private RecommendedResourceDto MapReaderToRecommended(System.Data.Common.DbDataReader reader, string defaultReason)
    {
        var scoreOrdinal = reader.GetOrdinal("Score");
        var avgRatingOrdinal = reader.GetOrdinal("AvgRating");
        return new RecommendedResourceDto
        {
            ResourceId = reader.GetGuid(reader.GetOrdinal("Id")),
            ResourceName = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ResourceType = reader.GetInt32(reader.GetOrdinal("ResourceType")),
            CategoryId = reader.IsDBNull(reader.GetOrdinal("CategoryId")) ? null : reader.GetGuid(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
            Keywords = reader.IsDBNull(reader.GetOrdinal("Keywords")) ? null : reader.GetString(reader.GetOrdinal("Keywords")),
            FileExtension = reader.IsDBNull(reader.GetOrdinal("FileExtension")) ? null : reader.GetString(reader.GetOrdinal("FileExtension")),
            FileSize = reader.IsDBNull(reader.GetOrdinal("FileSize")) ? null : reader.GetInt64(reader.GetOrdinal("FileSize")),
            ViewCount = reader.GetInt32(reader.GetOrdinal("ViewCount")),
            CollectionCount = reader.GetInt32(reader.GetOrdinal("CollectionCount")),
            DownloadCount = reader.GetInt32(reader.GetOrdinal("DownloadCount")),
            AverageRating = reader.IsDBNull(avgRatingOrdinal) ? 0 : Math.Round(reader.GetDouble(avgRatingOrdinal), 1),
            TotalReviews = reader.GetInt32(reader.GetOrdinal("TotalReviews")),
            RecommendationScore = reader.IsDBNull(scoreOrdinal) ? 0 : Math.Round(reader.GetDouble(scoreOrdinal), 4),
            RecommendationReason = defaultReason,
            CreationTime = reader.GetDateTime(reader.GetOrdinal("CreationTime"))
        };
    }
}
