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
    /// <summary>
    /// 本章包含的子章节（勾选父章节时不会自动勾子章节，但子章节内容随本章一起进入生成提示词）。
    /// </summary>
    public List<CourseSubChapterDto>? SubChapters { get; set; }
    /// <summary>案例主题提示（如「跨境电商定价失误」）。</summary>
    public string? TopicHint { get; set; }
    /// <summary>教师附加要求（出题方向 / 行业限定 / 难度等）。</summary>
    public string? CustomPrompt { get; set; }
}

public class CaseAnalysisExportInputDto
{
    public string CaseAnalysisJson { get; set; } = string.Empty;
}

/// <summary>
/// 课程多章节案例生成输入（案例库：按课程勾选的多个章节逐章生成，每章一条案例）。
/// </summary>
public class CourseMultiCaseAnalysisInputDto
{
    /// <summary>所属课程 Id。</summary>
    public Guid CourseId { get; set; }
    /// <summary>课程名称。</summary>
    public string CourseTitle { get; set; } = string.Empty;
    /// <summary>案例主题提示（默认主题，可被章节级覆盖）。</summary>
    public string? TopicHint { get; set; }
    /// <summary>分析关注点（默认关注点，可被章节级覆盖）。</summary>
    public string? FocusArea { get; set; }
    /// <summary>教师附加要求（默认要求，可被章节级覆盖）。</summary>
    public string? CustomPrompt { get; set; }
    /// <summary>参考资源（可选，课程内已关联的教学资源）。</summary>
    public Guid? ResourceId { get; set; }
    /// <summary>待生成的章节（教师预览确认后的结果，按顺序逐章生成）。</summary>
    public List<CourseCaseChapterItemDto> Chapters { get; set; } = new();
}

/// <summary>待生成的一个章节（来自课程章节树勾选结果）。</summary>
public class CourseCaseChapterItemDto
{
    /// <summary>章节 Id（落库时作为案例的章节归属）。</summary>
    public Guid ChapterId { get; set; }
    /// <summary>生成顺序（从 1 开始）。</summary>
    public int Order { get; set; }
    /// <summary>章节标题（可编辑）。</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>章节要点 / 描述（案例生成依据，可编辑）。</summary>
    public string? Summary { get; set; }
    /// <summary>本章案例主题提示（可空，为空时用输入级默认值）。</summary>
    public string? TopicHint { get; set; }
    /// <summary>本章分析关注点（可空，为空时用输入级默认值）。</summary>
    public string? FocusArea { get; set; }
    /// <summary>本章教师附加要求（可空，为空时用输入级默认值）。</summary>
    public string? CustomPrompt { get; set; }
    /// <summary>本章参考资源（可空，为空时用输入级默认值）。</summary>
    public Guid? ResourceId { get; set; }
    /// <summary>本章包含的子章节（内容随本章一起进入生成提示词）。</summary>
    public List<CourseSubChapterDto>? SubChapters { get; set; }
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
