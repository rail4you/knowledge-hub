using System.Text.Json.Serialization;

namespace KnowledgeHub.AI.Api.Models;

public record LessonPlan
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("subject")]
    public required string Subject { get; init; }
    
    [JsonPropertyName("grade")]
    public required string Grade { get; init; }
    
    [JsonPropertyName("duration")]
    public required int Duration { get; init; }
    
    [JsonPropertyName("teachingObjectives")]
    public required string[] TeachingObjectives { get; init; }
    
    [JsonPropertyName("keyPoints")]
    public required string[] KeyPoints { get; init; }
    
    [JsonPropertyName("difficulties")]
    public required string[] Difficulties { get; init; }
    
    [JsonPropertyName("sections")]
    public required TeachingSection[] Sections { get; init; }
    
    [JsonPropertyName("teachingMethods")]
    public required string[] TeachingMethods { get; init; }
    
    [JsonPropertyName("resources")]
    public required string[] Resources { get; init; }
    
    [JsonPropertyName("assessmentMethods")]
    public required string[] AssessmentMethods { get; init; }
    
    [JsonPropertyName("homework")]
    public required string[] Homework { get; init; }
    
    [JsonPropertyName("reflection")]
    public required string Reflection { get; init; }
}

public record TeachingSection
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("duration")]
    public required int Duration { get; init; }
    
    [JsonPropertyName("content")]
    public required string Content { get; init; }
    
    [JsonPropertyName("activities")]
    public required string[] Activities { get; init; }
    
    [JsonPropertyName("teacherActions")]
    public required string[] TeacherActions { get; init; }
    
    [JsonPropertyName("studentActions")]
    public required string[] StudentActions { get; init; }
}

public record LessonPlanInput
{
    public required string Topic { get; init; }
    public string? Subject { get; init; }
    public string? Grade { get; init; }
    public int Duration { get; init; } = 45;
    public string[]? Objectives { get; init; }
    public string[]? KeyPoints { get; init; }
}
