using System.Text.Json.Serialization;

namespace KnowledgeHub.AI.Api.Models;

public record Feedback
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("agentName")]
    public required string AgentName { get; init; }
    
    [JsonPropertyName("rating")]
    public required int Rating { get; init; }
    
    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
    
    [JsonPropertyName("category")]
    public required string Category { get; init; }
    
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }
    
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
}

public record FeedbackInput
{
    public required string SessionId { get; init; }
    public required string AgentName { get; init; }
    public required int Rating { get; init; }
    public string? Comment { get; init; }
    public required string Category { get; init; }
}

public record FeedbackStats
{
    [JsonPropertyName("totalFeedback")]
    public required int TotalFeedback { get; init; }
    
    [JsonPropertyName("averageRating")]
    public required double AverageRating { get; init; }
    
    [JsonPropertyName("ratingDistribution")]
    public required Dictionary<int, int> RatingDistribution { get; init; }
    
    [JsonPropertyName("categoryStats")]
    public required Dictionary<string, CategoryStat> CategoryStats { get; init; }
}

public record CategoryStat
{
    [JsonPropertyName("count")]
    public required int Count { get; init; }
    
    [JsonPropertyName("averageRating")]
    public required double AverageRating { get; init; }
}

public record ModelInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("version")]
    public required string Version { get; init; }
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    
    [JsonPropertyName("fileSize")]
    public required long FileSize { get; init; }
    
    [JsonPropertyName("uploadedAt")]
    public required DateTime UploadedAt { get; init; }
    
    [JsonPropertyName("uploadedBy")]
    public string? UploadedBy { get; init; }
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
