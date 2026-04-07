using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

public class CareerGuidanceGenerationInputDto
{
    public Guid ResourceId { get; set; }
    public string? CareerGoal { get; set; }
}

public class CareerGuidanceExportInputDto
{
    public string CareerGuidanceJson { get; set; } = string.Empty;
}

public class CareerGuidanceDto
{
    public string Title { get; set; } = string.Empty;
    public CareerAssessmentDto Assessment { get; set; } = new();
    public List<CareerPathDto> RecommendedPaths { get; set; } = new();
    public List<SkillGapDto> SkillGaps { get; set; } = new();
    public List<ActionPlanItemDto> ActionPlan { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
}

public class CareerAssessmentDto
{
    public int CareerMatchScore { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> AreasForImprovement { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class CareerPathDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public string SalaryRange { get; set; } = string.Empty;
    public string GrowthPotential { get; set; } = string.Empty;
}

public class SkillGapDto
{
    public string Skill { get; set; } = string.Empty;
    public string CurrentLevel { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

public class ActionPlanItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timeline { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
