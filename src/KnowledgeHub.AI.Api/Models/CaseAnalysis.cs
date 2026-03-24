using System.Text.Json.Serialization;

namespace KnowledgeHub.AI.Api.Models;

public record CaseAnalysis
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }
    
    [JsonPropertyName("background")]
    public required BackgroundAnalysis Background { get; init; }
    
    [JsonPropertyName("keyIssues")]
    public required KeyIssue[] KeyIssues { get; init; }
    
    [JsonPropertyName("solutions")]
    public required Solution[] Solutions { get; init; }
    
    [JsonPropertyName("keyInsights")]
    public required string[] KeyInsights { get; init; }
    
    [JsonPropertyName("recommendations")]
    public required string[] Recommendations { get; init; }
}

public record BackgroundAnalysis
{
    [JsonPropertyName("industry")]
    public required string Industry { get; init; }
    
    [JsonPropertyName("context")]
    public required string Context { get; init; }
    
    [JsonPropertyName("stakeholders")]
    public required string[] Stakeholders { get; init; }
    
    [JsonPropertyName("timeframe")]
    public required string Timeframe { get; init; }
}

public record KeyIssue
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("impact")]
    public required string Impact { get; init; }
    
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }
}

public record Solution
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("steps")]
    public required string[] Steps { get; init; }
    
    [JsonPropertyName("expectedOutcome")]
    public required string ExpectedOutcome { get; init; }
    
    [JsonPropertyName("relatedIssues")]
    public required string[] RelatedIssues { get; init; }
}

public record CaseAnalysisInput
{
    public required string Content { get; init; }
    public string? Title { get; init; }
    public string[]? FocusAreas { get; init; }
}
