using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

/// <summary>案例内容里的案例背景（与 AI 中心案例分析 schema 对齐）。</summary>
public class CourseCaseBackgroundDto
{
    public string Industry { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<string> Stakeholders { get; set; } = new();
}

/// <summary>关键问题。</summary>
public class CourseCaseKeyIssueDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;

    /// <summary>严重程度：高 / 中 / 低。</summary>
    public string Severity { get; set; } = string.Empty;
}

/// <summary>解决方案。</summary>
public class CourseCaseSolutionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string ExpectedOutcome { get; set; } = string.Empty;
}

/// <summary>
/// 结构化案例内容。手动录入、Excel 导入、AI 生成三种来源共用同一套结构，
/// 后端序列化为 JSON 存入 <see cref="CourseCase.ContentJson"/>。
/// </summary>
public class CourseCaseContentDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public CourseCaseBackgroundDto Background { get; set; } = new();
    public List<CourseCaseKeyIssueDto> KeyIssues { get; set; } = new();
    public List<CourseCaseSolutionDto> Solutions { get; set; } = new();
    public List<string> KeyInsights { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class CourseCaseDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Industry { get; set; }
    public string? Timeframe { get; set; }
    public string? ContentJson { get; set; }

    /// <summary>解析后的结构化内容（ContentJson 为空时为默认空结构）。</summary>
    public CourseCaseContentDto Content { get; set; } = new();

    public CourseCaseSource Source { get; set; }
    public Guid? AiTaskId { get; set; }
    public string? CreatorName { get; set; }
}

public class CourseCaseQueryDto : PagedAndSortedResultRequestDto
{
    public Guid? CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public CourseCaseSource? Source { get; set; }
    public string? Filter { get; set; }
}

public class CreateCourseCaseDto
{
    [Required]
    public Guid CourseId { get; set; }

    public Guid? ChapterId { get; set; }

    [StringLength(256)]
    public string? ChapterTitle { get; set; }

    [Required]
    [StringLength(256)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Summary { get; set; }

    /// <summary>结构化内容；为空时仅保存标题 + 摘要（草稿态案例）。</summary>
    public CourseCaseContentDto? Content { get; set; }
}

public class UpdateCourseCaseDto
{
    [Required]
    [StringLength(256)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Summary { get; set; }

    public Guid? ChapterId { get; set; }

    [StringLength(256)]
    public string? ChapterTitle { get; set; }

    public CourseCaseContentDto? Content { get; set; }
}

/// <summary>案例导入结果。同一 Excel 文件可含多个案例，按行解析。</summary>
public class CourseCaseImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
