using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

public class CaseAnalysisGenerationInputDto
{
    /// <summary>
    /// 教学依据资源（可空）。课程案例库生成时直接基于课程章节信息，不强制关联资源。
    /// </summary>
    public Guid? ResourceId { get; set; }
    public string? FocusArea { get; set; }

    // ── 课程案例库（来源：课程 → 案例库）：基于课程章节生成，不再要求资源 ──
    /// <summary>所属课程 Id（非空时按课程案例处理并落库到案例库）。</summary>
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    /// <summary>章节内容要点 / 描述，作为课程案例的生成依据。</summary>
    public string? ChapterSummary { get; set; }
    /// <summary>案例主题提示（如「跨境电商定价失误」）。</summary>
    public string? TopicHint { get; set; }
    /// <summary>教师附加要求（出题方向 / 行业限定 / 难度等）。</summary>
    public string? CustomPrompt { get; set; }
}

public class CaseAnalysisExportInputDto
{
    public string CaseAnalysisJson { get; set; } = string.Empty;
}

public class CaseAnalysisDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public CaseBackgroundDto Background { get; set; } = new();
    public List<CaseKeyIssueDto> KeyIssues { get; set; } = new();
    public List<CaseSolutionDto> Solutions { get; set; } = new();
    public List<string> KeyInsights { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class CaseBackgroundDto
{
    public string Industry { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<string> Stakeholders { get; set; } = new();
}

public class CaseKeyIssueDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public class CaseSolutionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string ExpectedOutcome { get; set; } = string.Empty;
}
