using System;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class RecommendedResourceDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Keywords { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public int ViewCount { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public double RecommendationScore { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
}

public class ResourceStatisticsDto
{
    public Guid ResourceId { get; set; }
    public int TotalViews { get; set; }
    public int UniqueViewers { get; set; }
    public double AvgViewDurationSeconds { get; set; }
    public int TotalDownloads { get; set; }
    public int TotalCollections { get; set; }
    public double CollectionRate { get; set; }
    public double DownloadRate { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int[] RatingDistribution { get; set; } = new int[5];
    public int ViewsLast30Days { get; set; }
    public int ViewsPrevious30Days { get; set; }
    public double ViewTrendPercentage { get; set; }
    public int TimesInSearchResults { get; set; }
    public int TimesClickedFromSearch { get; set; }
    public double ClickThroughRate { get; set; }
}
