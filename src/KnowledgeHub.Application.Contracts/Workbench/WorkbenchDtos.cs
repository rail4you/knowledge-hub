using System;
using System.Collections.Generic;

namespace KnowledgeHub.Workbench;

/// <summary>
/// 系统工作台查询参数。
/// </summary>
public class WorkbenchQueryDto
{
    /// <summary>
    /// 目标租户 Id。仅 host 全局管理员可指定：
    /// - 指定租户：查看该租户统计；
    /// - 为空：汇总全部租户统计。
    /// 租户上下文用户忽略此参数，恒为当前租户。
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 系统工作台统计（按模块分组）。所有计数均为当前租户范围内的实时值。
/// </summary>
public class WorkbenchStatsDto
{
    public Guid? TenantId { get; set; }

    /// <summary>租户名；host 汇总时为“全部租户”。</summary>
    public string? TenantName { get; set; }

    public DateTime GeneratedAt { get; set; }

    public WorkbenchResourceStatsDto Resources { get; set; } = new();

    public WorkbenchCourseStatsDto Courses { get; set; } = new();

    public WorkbenchAiStatsDto Ai { get; set; } = new();

    public WorkbenchEmploymentStatsDto Employment { get; set; } = new();

    public WorkbenchPracticumStatsDto Practicum { get; set; } = new();

    public WorkbenchNewsStatsDto News { get; set; } = new();

    public WorkbenchSearchStatsDto Search { get; set; } = new();

    public WorkbenchUserStatsDto Users { get; set; } = new();

    /// <summary>基于时间的近 7 天使用趋势。</summary>
    public WorkbenchTrendsDto Trends { get; set; } = new();
}

/// <summary>近 7 天使用趋势：逐日明细 + 今天/昨天/近 7 天汇总。</summary>
public class WorkbenchTrendsDto
{
    /// <summary>近 7 天逐日使用量（含今天，按日期升序）。</summary>
    public List<WorkbenchDailyUsageDto> Daily { get; set; } = new();

    public WorkbenchUsageDto Today { get; set; } = new();

    public WorkbenchUsageDto Yesterday { get; set; } = new();

    /// <summary>近 7 天合计。</summary>
    public WorkbenchUsageDto LastDays { get; set; } = new();
}

/// <summary>某时间段内的各模块使用量。</summary>
public class WorkbenchUsageDto
{
    public long Searches { get; set; }
    public long ResourceUploads { get; set; }
    public long ResourceViews { get; set; }
    public long AiCalls { get; set; }
    public long Enrollments { get; set; }
    public long JobApplications { get; set; }
    public long PracticumSubmissions { get; set; }
}

/// <summary>单日使用量。</summary>
public class WorkbenchDailyUsageDto : WorkbenchUsageDto
{
    public DateTime Date { get; set; }

    /// <summary>展示用短标签（MM-dd）。</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>资源管理统计。</summary>
public class WorkbenchResourceStatsDto
{
    public long Total { get; set; }
    public long Draft { get; set; }
    public long PendingReview { get; set; }

    /// <summary>已通过（校级 / 联盟审核通过）。</summary>
    public long Approved { get; set; }

    public long Rejected { get; set; }
    public long CategoryCount { get; set; }
    public long TotalDownloads { get; set; }
    public long TotalViews { get; set; }
    public long TotalCollections { get; set; }
}

/// <summary>专业和课程管理统计。</summary>
public class WorkbenchCourseStatsDto
{
    public long Total { get; set; }
    public long Published { get; set; }
    public long PendingReview { get; set; }
    public long Draft { get; set; }
    public long ChapterCount { get; set; }
    public long ExerciseCount { get; set; }
    public long MajorCount { get; set; }
    public long MicroMajorCount { get; set; }
    public long MicroMajorPublished { get; set; }

    /// <summary>选课人次（未退课）。</summary>
    public long EnrollmentCount { get; set; }

    /// <summary>微专业报名人次。</summary>
    public long MicroMajorEnrollmentCount { get; set; }
}

/// <summary>AI 管理统计。</summary>
public class WorkbenchAiStatsDto
{
    public long TotalCalls { get; set; }
    public long SuccessCalls { get; set; }
    public long FailedCalls { get; set; }
    public long RunningCalls { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public decimal EstimatedCost { get; set; }

    /// <summary>AI 异步生成任务总数。</summary>
    public long TaskCount { get; set; }
}

/// <summary>就业管理统计。</summary>
public class WorkbenchEmploymentStatsDto
{
    public long JobTotal { get; set; }
    public long JobPublished { get; set; }
    public long ApplicationCount { get; set; }
    public long InterviewCount { get; set; }
    public long OutcomeCount { get; set; }

    /// <summary>已签约。</summary>
    public long SignedCount { get; set; }

    /// <summary>已就业。</summary>
    public long EmployedCount { get; set; }
}

/// <summary>实训统计。</summary>
public class WorkbenchPracticumStatsDto
{
    public long ProjectTotal { get; set; }
    public long ProjectPublished { get; set; }
    public long TaskCount { get; set; }
    public long EnrollmentCount { get; set; }
    public long SubmissionCount { get; set; }
}

/// <summary>资讯管理统计。</summary>
public class WorkbenchNewsStatsDto
{
    public long Total { get; set; }
    public long Published { get; set; }
    public long PendingReview { get; set; }
}

/// <summary>检索统计。</summary>
public class WorkbenchSearchStatsDto
{
    public long TotalSearches { get; set; }
    public long TodaySearches { get; set; }
    public long ActiveUsers { get; set; }
}

/// <summary>用户统计。</summary>
public class WorkbenchUserStatsDto
{
    /// <summary>选课学生数（去重）。</summary>
    public long StudentCount { get; set; }

    /// <summary>授课教师数（去重）。</summary>
    public long TeacherCount { get; set; }
}
