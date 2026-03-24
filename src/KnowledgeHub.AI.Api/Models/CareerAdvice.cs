using System.Text.Json.Serialization;

namespace KnowledgeHub.AI.Api.Models;

public record CareerAdvice
{
    [JsonPropertyName("assessment")]
    public required PersonalAssessment Assessment { get; init; }
    
    [JsonPropertyName("recommendedPaths")]
    public required CareerPath[] RecommendedPaths { get; init; }
    
    [JsonPropertyName("skillGaps")]
    public required SkillGap[] SkillGaps { get; init; }
    
    [JsonPropertyName("actionPlan")]
    public required ActionItem[] ActionPlan { get; init; }
    
    [JsonPropertyName("resources")]
    public required LearningResource[] Resources { get; init; }
    
    [JsonPropertyName("nextSteps")]
    public required string[] NextSteps { get; init; }
}

public record PersonalAssessment
{
    [JsonPropertyName("strengths")]
    public required string[] Strengths { get; init; }
    
    [JsonPropertyName("areasForImprovement")]
    public required string[] AreasForImprovement { get; init; }
    
    [JsonPropertyName("interests")]
    public required string[] Interests { get; init; }
    
    [JsonPropertyName("careerMatchScore")]
    public required int CareerMatchScore { get; init; }
}

public record CareerPath
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("matchScore")]
    public required int MatchScore { get; init; }
    
    [JsonPropertyName("requiredSkills")]
    public required string[] RequiredSkills { get; init; }
    
    [JsonPropertyName("salaryRange")]
    public required string SalaryRange { get; init; }
    
    [JsonPropertyName("growthPotential")]
    public required string GrowthPotential { get; init; }
    
    [JsonPropertyName("entryRequirements")]
    public required string[] EntryRequirements { get; init; }
}

public record SkillGap
{
    [JsonPropertyName("skill")]
    public required string Skill { get; init; }
    
    [JsonPropertyName("currentLevel")]
    public required string CurrentLevel { get; init; }
    
    [JsonPropertyName("targetLevel")]
    public required string TargetLevel { get; init; }
    
    [JsonPropertyName("priority")]
    public required string Priority { get; init; }
    
    [JsonPropertyName("learningResources")]
    public required string[] LearningResources { get; init; }
}

public record ActionItem
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("timeline")]
    public required string Timeline { get; init; }
    
    [JsonPropertyName("priority")]
    public required string Priority { get; init; }
}

public record LearningResource
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("link")]
    public string? Link { get; init; }
}

public record StudentProfile
{
    public string? Major { get; init; }
    public string? Grade { get; init; }
    public string[]? Skills { get; init; }
    public double? Gpa { get; init; }
    public string[]? Interests { get; init; }
    public string[]? Certifications { get; init; }
    public string[]? Projects { get; init; }
    public string? CareerGoal { get; init; }
}
