using System;
using System.Collections.Generic;
using KnowledgeHub.CourseIdeology.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.CourseIdeology.Dtos;

// ══════════════════════════════════════════════════════════
//  思政元素
// ══════════════════════════════════════════════════════════

public class IdeologyElementDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>关联案例数（列表页展示用）。</summary>
    public int CaseCount { get; set; }
}

public class CreateUpdateIdeologyElementDto
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}

// ══════════════════════════════════════════════════════════
//  思政案例
// ══════════════════════════════════════════════════════════

/// <summary>案例列表行 / 详情（含元素名，便于前端直接展示标签）。</summary>
public class IdeologyCaseDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ContentJson { get; set; }
    public IdeologySourceType SourceType { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public DateTime? SourceDate { get; set; }
    public IdeologyUsageMethod UsageMethod { get; set; }
    public IdeologyCaseStatus Status { get; set; }
    public bool AiGenerated { get; set; }
    public string? AiMaterial { get; set; }
    public string? CreatorName { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreationTime { get; set; }

    public List<Guid> ElementIds { get; set; } = new();
    public List<string> ElementNames { get; set; } = new();
}

public class CreateUpdateIdeologyCaseDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ContentJson { get; set; }
    public IdeologySourceType SourceType { get; set; } = IdeologySourceType.Textbook;

    /// <summary>出处（必填）。</summary>
    public string SourceText { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }
    public DateTime? SourceDate { get; set; }
    public IdeologyUsageMethod UsageMethod { get; set; } = IdeologyUsageMethod.Import;
    public List<Guid> ElementIds { get; set; } = new();

    /// <summary>是否由 AI 生成（AI 面板生成时置 true）。</summary>
    public bool AiGenerated { get; set; }

    /// <summary>AI 生成时的素材快照。</summary>
    public string? AiMaterial { get; set; }

    /// <summary>保存后是否直接发布（默认发布：本模块不做审核，老师自建自用）。</summary>
    public bool Publish { get; set; } = true;
}

public class IdeologyCaseQueryDto : PagedAndSortedResultRequestDto
{
    /// <summary>课程 Id（必填：思政内容一律挂在课程上）。</summary>
    public Guid CourseId { get; set; }

    public Guid? ChapterId { get; set; }
    public Guid? ElementId { get; set; }
    public IdeologyCaseStatus? Status { get; set; }

    /// <summary>按标题 / 出处 / 说明模糊搜索。</summary>
    public string? Filter { get; set; }
}

// ══════════════════════════════════════════════════════════
//  图谱
// ══════════════════════════════════════════════════════════

public class IdeologyGraphNodeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>附加信息：章节=案例数；案例=状态；元素=案例数。</summary>
    public int Weight { get; set; }

    public IdeologyCaseStatus? Status { get; set; }
}

public class IdeologyGraphEdgeDto
{
    /// <summary><c>case-chapter</c> 或 <c>case-element</c>。</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid From { get; set; }
    public Guid To { get; set; }
}

public class IdeologyGraphDto
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public List<IdeologyGraphNodeDto> Chapters { get; set; } = new();
    public List<IdeologyGraphNodeDto> Cases { get; set; } = new();
    public List<IdeologyGraphNodeDto> Elements { get; set; } = new();
    public List<IdeologyGraphEdgeDto> Edges { get; set; } = new();

    /// <summary>缺口：一个已发布案例都没有的章节。</summary>
    public List<IdeologyGraphNodeDto> Gaps { get; set; } = new();
}

// ══════════════════════════════════════════════════════════
//  覆盖分析
// ══════════════════════════════════════════════════════════

public class IdeologyCoverageItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>已发布案例数。</summary>
    public int CaseCount { get; set; }

    /// <summary>占比（0~100，保留 1 位小数）。</summary>
    public decimal Percent { get; set; }
}

public class IdeologyCoverageDto
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int TotalCases { get; set; }
    public int PublishedCases { get; set; }
    public int DraftCases { get; set; }
    public int ArchivedCases { get; set; }
    public int AiGeneratedCases { get; set; }

    /// <summary>课程思政总目标（来自课程设置，可空）。</summary>
    public string? IdeologyGoal { get; set; }

    public List<IdeologyCoverageItemDto> Chapters { get; set; } = new();
    public List<IdeologyCoverageItemDto> Elements { get; set; } = new();

    /// <summary>盲区提示（无已发布案例的章节 / 零案例的元素）。</summary>
    public List<string> Gaps { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
}

// ══════════════════════════════════════════════════════════
//  课程设置
// ══════════════════════════════════════════════════════════

public class CourseIdeologySettingDto
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public bool IsEnabled { get; set; }
    public string? IdeologyGoal { get; set; }
    public string? Note { get; set; }
    public int CaseCount { get; set; }
}

public class SaveCourseIdeologySettingDto
{
    public Guid CourseId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? IdeologyGoal { get; set; }
    public string? Note { get; set; }
}

// ══════════════════════════════════════════════════════════
//  教案融入
// ══════════════════════════════════════════════════════════

public class IdeologyInsetDto
{
    public Guid Id { get; set; }
    public Guid LessonPlanId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? ElementId { get; set; }
    public IdeologyInsetPosition Position { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int SortOrder { get; set; }

    /// <summary>案例出处（引用案例时回填，导出材料要用）。</summary>
    public string? SourceText { get; set; }
}

public class CreateIdeologyInsetDto
{
    public Guid LessonPlanId { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? ElementId { get; set; }
    public IdeologyInsetPosition Position { get; set; } = IdeologyInsetPosition.Import;
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>教案「推荐思政点」：按教案所属章节推荐的已发布案例。</summary>
public class IdeologyRecommendItemDto
{
    public Guid CaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public IdeologyUsageMethod UsageMethod { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public List<string> ElementNames { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════
//  AI 生成（同步返回草稿，由教师确认后保存）
// ══════════════════════════════════════════════════════════

public class GenerateIdeologyCaseInputDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }

    /// <summary>希望覆盖的思政元素（可多选）。</summary>
    public List<Guid> ElementIds { get; set; } = new();

    /// <summary>教师提供的素材 / 链接（**必须提供**，AI 不得凭空编造事实）。</summary>
    public string Material { get; set; } = string.Empty;

    public IdeologyUsageMethod UsageMethod { get; set; } = IdeologyUsageMethod.Import;

    /// <summary>附加要求（可选）。</summary>
    public string? ExtraPrompt { get; set; }
}

public class IdeologyCaseDraftDto
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }

    /// <summary>结构化内容 JSON（entryPoint / brief / discussionQuestions / integrationSuggestion）。</summary>
    public string? ContentJson { get; set; }

    public string SourceText { get; set; } = string.Empty;

    /// <summary>必须人工核验的提醒项。</summary>
    public List<string> Warnings { get; set; } = new();
}
