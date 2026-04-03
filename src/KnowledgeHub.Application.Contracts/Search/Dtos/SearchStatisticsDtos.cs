using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class SearchDashboardDto
{
    public SearchStatsBreakdown All { get; set; } = new();
    public SearchStatsBreakdown Document { get; set; } = new();
    public SearchStatsBreakdown Video { get; set; } = new();
    public List<DailySearchTrendDto> DailyTrends { get; set; } = new();
    public List<PopularSearchTermDto> PopularSearches { get; set; } = new();
    public List<TopResourceStatsDto> TopResources { get; set; } = new();
    public List<TopRatedResourceDto> TopRatedResources { get; set; } = new();
}

public class SearchStatsBreakdown
{
    public long TotalSearches { get; set; }
    public long TodaySearches { get; set; }
    public int ActiveUsers { get; set; }
    public int TodayActiveUsers { get; set; }
}

public class SearchStatsQueryDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? TenantId { get; set; }
}

public class DailySearchTrendDto
{
    public DateTime Date { get; set; }
    public int TotalSearchCount { get; set; }
    public int DocumentSearchCount { get; set; }
    public int VideoSearchCount { get; set; }
    public int UniqueUsers { get; set; }
}

public class PopularSearchTermDto
{
    public string Keyword { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>"document" | "video" | "all"</summary>
    public string SourceType { get; set; } = "all";
}

public class TopResourceStatsDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public int SearchCount { get; set; }
    public int ClickCount { get; set; }
    public double ClickRate { get; set; }
}

public class TopRatedResourceDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
