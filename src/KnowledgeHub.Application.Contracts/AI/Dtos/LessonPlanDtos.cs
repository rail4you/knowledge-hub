using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

public class LessonPlanGenerationInputDto
{
    public Guid ResourceId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Grade { get; set; }
    public int Duration { get; set; } = 45;
}

public class LessonPlanExportInputDto
{
    public string LessonPlanJson { get; set; } = string.Empty;
}

public class TeachingSectionDto
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Activities { get; set; } = new();
}

public class LessonPlanDto
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int Duration { get; set; }
    public List<string> Objectives { get; set; } = new();
    public List<string> KeyPoints { get; set; } = new();
    public List<string> Difficulties { get; set; } = new();
    public List<TeachingSectionDto> Sections { get; set; } = new();
    public List<string> Methods { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public List<string> Assessment { get; set; } = new();
    public List<string> Homework { get; set; } = new();
}
