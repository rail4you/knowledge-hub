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
    /// <summary>
    /// 教师附加要求 / 特殊说明（可选），例如：强调课程思政、双语教学、工程实践、学情约束等。
    /// 非空时必须被教案严格遵循。
    /// </summary>
    public string? CustomPrompt { get; set; }
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
