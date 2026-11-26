using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Learning.Dtos;

/// <summary>
/// 学习画像与数据洞察（对齐 kg-edu 的 <c>student_profile</c> + <c>insights/engine</c>）。
/// <para>
/// 数据源只有一处：`AppStudentActivityLogs`（行为埋点）+ `AppKnowledgeMastery`（掌握度）。
/// 画像不是"手填标签"，而是从行为里算出来的：偏好类型、活跃时段、学习连续性、近期活跃度。
/// </para>
/// </summary>
public interface ILearningProfileAppService : IApplicationService
{
    /// <summary>学生看自己的画像（可指定课程）。</summary>
    Task<StudentLearningProfileDto> GetMyProfileAsync(Guid? courseId = null);

    /// <summary>教师/管理员看某个学生的画像。</summary>
    Task<StudentLearningProfileDto> GetStudentProfileAsync(Guid studentId, Guid? courseId = null);

    /// <summary>课程维度画像：KPI + 近 N 天趋势 + 行为分布 + 活跃学生排行。</summary>
    Task<CourseLearningProfileDto> GetCourseProfileAsync(Guid courseId, int days = 30);

    /// <summary>数据洞察：在课程画像基础上给出规则式结论（下降/薄弱集中/低活跃）。</summary>
    Task<CourseInsightDto> GetCourseInsightAsync(Guid courseId, int days = 30);

    /// <summary>按模板生成报告（页面预览 + 导出 xlsx 共用这份数据）。</summary>
    Task<ProfileReportDto> BuildReportAsync(Guid courseId, string template = "monthly", int days = 30);

    /// <summary>导出报告（xlsx）。</summary>
    Task<Volo.Abp.Content.IRemoteStreamContent> ExportReportAsync(Guid courseId, string template = "monthly", int days = 30);
}

public class StudentLearningProfileDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid? CourseId { get; set; }

    /// <summary>画像区间内的活动总条数。</summary>
    public int ActivityCount { get; set; }

    /// <summary>最近一次活动时间（判断"断档"）。</summary>
    public DateTime? LastActiveAt { get; set; }

    /// <summary>偏好学习方式（活动类型众数）：章节学习 / 作答习题 / 课堂练习 …</summary>
    public string? PreferredActionLabel { get; set; }
    public StudentActivityAction? PreferredAction { get; set; }

    /// <summary>活跃时段（0-5 深夜 / 6-11 上午 / 12-17 下午 / 18-23 晚上）。</summary>
    public string? ActivePeriod { get; set; }

    /// <summary>学习连续性：近 30 天有活动的天数 / 30（0~1）。</summary>
    public double ConsistencyScore { get; set; }

    /// <summary>近期活跃度：近 7 天活动量 / 前 7 天活动量（>1 说明在升温）。</summary>
    public double RecentActivityLevel { get; set; }

    /// <summary>画像结论（规则式，直接给学生看）。</summary>
    public List<string> Insights { get; set; } = new();

    /// <summary>行为分布（类型 → 次数），画柱状图用。</summary>
    public List<ProfileActionCountDto> ActionDistribution { get; set; } = new();

    /// <summary>薄弱知识点 Top5（掌握度低于判定线）。</summary>
    public List<ProfileWeakPointDto> WeakPoints { get; set; } = new();

    /// <summary>近 30 天活动时间线（按天）。</summary>
    public List<ProfileTrendPointDto> Timeline { get; set; } = new();
}

public class ProfileActionCountDto
{
    public StudentActivityAction Action { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ProfileWeakPointDto
{
    public Guid KnowledgeResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Mastery { get; set; }
    public double Evidence { get; set; }
}

public class ProfileTrendPointDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class CourseLearningProfileDto
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public int Days { get; set; }

    /// <summary>选课学生数（不含退课）。</summary>
    public int StudentCount { get; set; }

    /// <summary>区间内有活动的学生数。</summary>
    public int ActiveStudentCount { get; set; }

    /// <summary>区间内活动总数。</summary>
    public int ActivityCount { get; set; }

    /// <summary>人均活动数。</summary>
    public double ActivityPerStudent { get; set; }

    /// <summary>班级平均连续性（每个学生 30 天里有活动的天数比例，再取平均）。</summary>
    public double AverageConsistency { get; set; }

    /// <summary>近 7 天 vs 前 7 天的活动量比值。</summary>
    public double RecentActivityLevel { get; set; }

    public List<ProfileTrendPointDto> Timeline { get; set; } = new();
    public List<ProfileActionCountDto> ActionDistribution { get; set; } = new();
    public List<StudentActivitySummaryDto> TopActiveStudents { get; set; } = new();

    /// <summary>区间内没有任何活动的学生（催促名单）。</summary>
    public List<StudentActivitySummaryDto> InactiveStudents { get; set; } = new();
}

public class StudentActivitySummaryDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public int ActivityCount { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

public class CourseInsightDto
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public int Days { get; set; }

    /// <summary>指标卡（键/值/说明），前端直接渲染。</summary>
    public List<InsightMetricDto> Metrics { get; set; } = new();

    /// <summary>自动结论（规则式，按严重度排序）。</summary>
    public List<InsightFindingDto> Findings { get; set; } = new();
}

public class InsightMetricDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Hint { get; set; }
}

public class InsightFindingDto
{
    /// <summary>info / warning / danger</summary>
    public string Level { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public class ProfileReportDto
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string Template { get; set; } = "monthly";
    public string TemplateLabel { get; set; } = string.Empty;
    public int Days { get; set; }
    public DateTime GeneratedAt { get; set; }

    /// <summary>报告小节（标题 + 段落 + 可选表格）。</summary>
    public List<ProfileReportSectionDto> Sections { get; set; } = new();
}

public class ProfileReportSectionDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>段落文本（每条一段）。</summary>
    public List<string> Paragraphs { get; set; } = new();

    /// <summary>可选表格。</summary>
    public ProfileReportTableDto? Table { get; set; }
}

public class ProfileReportTableDto
{
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}
