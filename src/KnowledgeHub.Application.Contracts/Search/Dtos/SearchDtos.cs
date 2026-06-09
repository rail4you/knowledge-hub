using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class SearchQueryDto
{
    public string Query { get; set; } = string.Empty;
    public List<string>? FileExtensions { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SkipCount { get; set; } = 0;
    public int MaxResultCount { get; set; } = 20;
    public string Sorting { get; set; } = "relevance";
    public string? IndexName { get; set; }
    /// <summary>MeiliSearch status 过滤器，如 "2,3" 仅已审核资源。null/空则不过滤。</summary>
    public string? StatusFilter { get; set; }
}

public class HybridSearchQueryDto : SearchQueryDto
{
    public List<float>? QueryEmbedding { get; set; }
}

public class SearchResultDto
{
    public List<DocumentSearchResultDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, long>> Facets { get; set; } = new();
}

public class DocumentSearchResultDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? HighlightedContent { get; set; }
    public float RelevanceScore { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
    public string? CategoryName { get; set; }
    public DateTime UploadDate { get; set; }

    /// <summary>"document" | "video"</summary>
    public string SourceType { get; set; } = "document";
    public string? VideoId { get; set; }
    public string? VideoName { get; set; }
    public string? VideoUrl { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? EventDescription { get; set; }
}

public class IndexDocumentDto
{
    public Guid ResourceId { get; set; }
}

public class RefreshDocumentIndexDto
{
    public Guid ResourceId { get; set; }
}

public class IndexTaskResultDto
{
    public long TaskId { get; set; }
    public Guid DocumentIndexId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class IndexStatusDto
{
    public Guid DocumentIndexId { get; set; }
    public Guid ResourceId { get; set; }
    public int PageNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreationTime { get; set; }
}

public class LogViewDto
{
    public Guid ResourceId { get; set; }
    public int? PageNumber { get; set; }
    public int ViewDurationSeconds { get; set; }
    public int ViewSource { get; set; }
}

public class SearchStatsDto
{
    public int TotalSearches { get; set; }
    public int UniqueUsers { get; set; }
    public double AvgResultsPerSearch { get; set; }
    public List<SearchTrendDto> DailyTrends { get; set; } = new();
    public string? TopSearchTerm { get; set; }
}

public class SearchTrendDto
{
    public DateTime Date { get; set; }
    public int SearchCount { get; set; }
}

public class PopularSearchDto
{
    public string Query { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopResourceDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public int ExposureCount { get; set; }
    public int ClickCount { get; set; }
    public double ClickRate { get; set; }
}

public class SearchHistoryDto
{
    public Guid Id { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
    public int ResultCount { get; set; }
}
