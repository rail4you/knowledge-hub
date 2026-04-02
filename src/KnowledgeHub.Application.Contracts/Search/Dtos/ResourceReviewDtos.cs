using System;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class ResourceReviewDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Content { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateResourceReviewDto
{
    public Guid ResourceId { get; set; }
    public int Rating { get; set; }
    public string? Content { get; set; }
}

public class UpdateResourceReviewDto
{
    public int Rating { get; set; }
    public string? Content { get; set; }
}

public class ResourceRatingSummaryDto
{
    public Guid ResourceId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int[] RatingDistribution { get; set; } = new int[5];
    public ResourceReviewDto? MyReview { get; set; }
}
