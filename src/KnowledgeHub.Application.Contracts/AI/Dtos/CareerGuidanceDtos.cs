using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

public class CareerGuidanceGenerationInputDto
{
    public Guid? ResourceId { get; set; }
    public string? CareerGoal { get; set; }
    /// <summary>学生简历内容（学生端用，优先级高于 ResourceId）</summary>
    public string? ResumeContent { get; set; }
    /// <summary>学生简历标题</summary>
    public string? ResumeTitle { get; set; }
    /// <summary>简历附件的存储路径（如 .docx/.pdf），后端读取全文并交给 AI 解析</summary>
    public string? AttachmentUrl { get; set; }
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
    /// <summary>从简历中提取的教育背景，按时间倒序排列</summary>
    public List<EducationEntryDto> EducationBackground { get; set; } = new();
    /// <summary>从简历中提取的工作/实习经历，按时间倒序排列</summary>
    public List<WorkExperienceEntryDto> WorkExperience { get; set; } = new();
}

public class EducationEntryDto
{
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string Major { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
}

public class WorkExperienceEntryDto
{
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
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
