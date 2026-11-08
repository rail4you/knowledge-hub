using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

public class LessonPlanGenerationInputDto
{
    /// <summary>
    /// 教学依据资源（可空）。课程教案可直接基于课程章节信息生成，不强制关联资源。
    /// </summary>
    public Guid? ResourceId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Grade { get; set; }
    public int Duration { get; set; } = 45;
    /// <summary>
    /// 教师附加要求 / 特殊说明（可选），例如：强调课程思政、双语教学、工程实践、学情约束等。
    /// 非空时必须被教案严格遵循。
    /// </summary>
    public string? CustomPrompt { get; set; }

    // ── 课程教案（教案管理）：基于课程章节生成，不再要求学科门类 / 授课对象 ──
    /// <summary>所属课程 Id（非空时按课程教案处理并落库到教案管理）。</summary>
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    /// <summary>所属章节 Id。</summary>
    public Guid? ChapterId { get; set; }
    /// <summary>章节标题。</summary>
    public string? ChapterTitle { get; set; }
    /// <summary>章节内容要点 / 描述，作为课程教案的教学依据。</summary>
    public string? ChapterSummary { get; set; }
}

public class LessonPlanExportInputDto
{
    public string LessonPlanJson { get; set; } = string.Empty;
}

/// <summary>
/// 多章节模式：章节解析输入。
/// </summary>
public class LessonPlanChapterParseInputDto
{
    public Guid ResourceId { get; set; }
    /// <summary>
    /// 解析附加提示（可选），例如限定章节范围、命名风格等。
    /// </summary>
    public string? CustomPrompt { get; set; }
}

/// <summary>
/// 从文档中解析出的单个章节（可由用户在前端编辑后回传）。
/// </summary>
public class LessonPlanChapterDto
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// 章节内容要点 / 摘要，用于后续逐章生成教案。
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// 章节解析结果。
/// </summary>
public class ChapterParseResultDto
{
    public string CourseTitle { get; set; } = string.Empty;
    public List<LessonPlanChapterDto> Chapters { get; set; } = new();
}

/// <summary>
/// 多章节整体教案生成输入（章节来自前端预览编辑后的结果）。
/// </summary>
public class MultiChapterLessonPlanGenerationInputDto
{
    public Guid ResourceId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Grade { get; set; }
    /// <summary>单章课时（分钟）。</summary>
    public int Duration { get; set; } = 45;
    public string? CustomPrompt { get; set; }
    public List<LessonPlanChapterDto> Chapters { get; set; } = new();
}

/// <summary>
/// 课程多章节教案生成输入（教案管理）：教师在章节树中勾选若干章节，确认后逐章生成，
/// 每章产出一份独立教案并落库到「教案管理」。
/// </summary>
public class CourseMultiChapterLessonPlanInputDto
{
    /// <summary>所属课程 Id。</summary>
    public Guid CourseId { get; set; }
    /// <summary>课程名称。</summary>
    public string CourseTitle { get; set; } = string.Empty;
    /// <summary>单章课时（分钟）。</summary>
    public int Duration { get; set; } = 45;
    /// <summary>教师附加要求（可选），各章生成时都会带上。</summary>
    public string? CustomPrompt { get; set; }
    /// <summary>参考资源（可选）：课程内已关联的教学资源，作为生成时的辅助材料。</summary>
    public Guid? ResourceId { get; set; }
    /// <summary>待生成的章节（教师预览确认后的结果，按顺序逐章生成）。</summary>
    public List<CourseChapterLessonPlanItemDto> Chapters { get; set; } = new();
}

/// <summary>待生成的一个章节（来自课程章节树勾选结果）。</summary>
public class CourseChapterLessonPlanItemDto
{
    /// <summary>章节 Id（落库时作为教案的章节归属）。</summary>
    public Guid ChapterId { get; set; }
    /// <summary>生成顺序（从 1 开始）。</summary>
    public int Order { get; set; }
    /// <summary>章节标题。</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>章节要点 / 描述，作为该章教案的教学依据。</summary>
    public string? Summary { get; set; }
    /// <summary>
    /// 本章包含的子章节（勾选父章节时不会自动勾子章节，但子章节内容随本章一起进入生成提示词）。
    /// </summary>
    public List<CourseSubChapterDto>? SubChapters { get; set; }
    /// <summary>本章学时（分钟）；为空时用输入级默认值。</summary>
    public int? Duration { get; set; }
    /// <summary>本章参考资源（课程资源 Id）；为空时用输入级默认值。</summary>
    public Guid? ResourceId { get; set; }
    /// <summary>本章的教师附加要求；为空时用输入级默认值。</summary>
    public string? CustomPrompt { get; set; }
}

/// <summary>章节下的子章节（用于补全本章的生成提示词）。</summary>
public class CourseSubChapterDto
{
    /// <summary>子章节标题。</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>子章节相对父章节的路径（如「1.1 概述 / 1.1.1 起源」）。</summary>
    public string? Path { get; set; }
    /// <summary>子章节要点 / 描述。</summary>
    public string? Summary { get; set; }
}

/// <summary>
/// 课程总览（多章节生成第一步的中间产物）。
/// </summary>
public class CourseOverviewDto
{
    public string CourseTitle { get; set; } = string.Empty;
    public List<string> CourseObjectives { get; set; } = new();
}

/// <summary>
/// 单章在整体教案中的承载结构。
/// </summary>
public class LessonPlanChapterPlanDto
{
    public int Order { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public LessonPlanDto LessonPlan { get; set; } = new();
}

/// <summary>
/// 多章节整体教案（课程总览 + 每章独立教案）。
/// </summary>
public class MultiChapterLessonPlanDto
{
    public string CourseTitle { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    /// <summary>总课时（单章课时 × 章节数）。</summary>
    public int Duration { get; set; }
    public List<string> CourseObjectives { get; set; } = new();
    public List<LessonPlanChapterPlanDto> Chapters { get; set; } = new();
}

public class MultiChapterLessonPlanExportInputDto
{
    public string LessonPlanJson { get; set; } = string.Empty;
}

/// <summary>
/// 教案工作流流式事件。
/// </summary>
public class LessonPlanStreamEventDto
{
    /// <summary>增量文本（章节解析时为流式 JSON 片段）。</summary>
    public string? Content { get; set; }
    /// <summary>进度提示文案。</summary>
    public string? Message { get; set; }
    public int Progress { get; set; }
    /// <summary>当前正在生成的章节序号（从 1 开始，仅多章节生成时有值）。</summary>
    public int? ChapterIndex { get; set; }
    public int? ChapterTotal { get; set; }
    public bool IsComplete { get; set; }
    public bool IsError { get; set; }
    /// <summary>最终结果 JSON（完成后一次下发）。</summary>
    public string? ResultJson { get; set; }
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
