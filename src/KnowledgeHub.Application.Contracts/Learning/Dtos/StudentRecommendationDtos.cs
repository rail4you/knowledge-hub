using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Learning.Dtos;

/// <summary>
/// 学生个性化推荐（对齐 kg-edu 的 <c>learning_recommendation</c> + <c>recommendation_engine</c>）。
/// <para>
/// 与智学分析里 <c>RecommendationEngine</c>（负责抽题，算完即丢）不同：
/// 这里把建议**落库并跟踪生命周期**，学生端才有稳定的推荐清单，教师端才能看采纳率。
/// </para>
/// <para>推荐来源（三个，全部只读已有数据，不引入新口径）：</para>
/// <list type="number">
/// <item>薄弱知识点：`KnowledgeMastery.Mastery` 低于课程判定线（且有证据）；</item>
/// <item>前置知识：`KpPrerequisite`（Confirmed）里前置未掌握的点；</item>
/// <item>未完成章节：`LearningProgress` 进度 &lt; 100% 且近期无活动。</item>
/// </list>
/// </summary>
public interface IStudentRecommendationAppService : IApplicationService
{
    /// <summary>学生端：我的推荐（默认只看未完成/未忽略的）。</summary>
    Task<List<StudentRecommendationDto>> GetMyAsync(Guid? courseId = null, bool includeHandled = false);

    /// <summary>学生端：状态流转（viewed / in_progress / completed / dismissed）。</summary>
    Task<StudentRecommendationDto> MarkAsync(Guid id, StudentRecommendationStatus status);

    /// <summary>学生端：按当前数据重新生成我的推荐（幂等，返回本次新增数）。</summary>
    Task<RecommendationRefreshResultDto> RefreshMineAsync(Guid courseId);

    /// <summary>教师端：课程推荐概览（分布 / 采纳率 / 学生维度清单）。</summary>
    Task<CourseRecommendationOverviewDto> GetCourseOverviewAsync(Guid courseId);

    /// <summary>教师端：为整门课的学生批量刷新推荐（作业量大时放后台，这里同步跑 + 限流）。</summary>
    Task<RecommendationRefreshResultDto> RefreshCourseAsync(Guid courseId);
}

public class StudentRecommendationDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid CourseId { get; set; }
    public StudentRecommendationType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public StudentRecommendationStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? KnowledgeResourceId { get; set; }
    public string? KnowledgeResourceName { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public double? MetricValue { get; set; }
    public DateTime? HandledAt { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>建议动作（前端按它决定跳转到练习 / 章节学习）。</summary>
    public string ActionKind { get; set; } = "practice";
}

public class RecommendationRefreshResultDto
{
    public Guid CourseId { get; set; }
    public int StudentCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ActiveCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CourseRecommendationOverviewDto
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }

    /// <summary>推荐总条数（当前未完成的）。</summary>
    public int ActiveCount { get; set; }

    /// <summary>已完成条数。</summary>
    public int CompletedCount { get; set; }

    /// <summary>已忽略条数。</summary>
    public int DismissedCount { get; set; }

    /// <summary>采纳率 = 完成 / (完成 + 忽略 + 未完成)。</summary>
    public double AdoptRate { get; set; }

    /// <summary>有推荐的学生数。</summary>
    public int StudentCount { get; set; }

    public List<RecommendationTypeCountDto> TypeDistribution { get; set; } = new();

    /// <summary>按学生聚合（前端列表）。</summary>
    public List<StudentRecommendationSummaryDto> Students { get; set; } = new();
}

public class RecommendationTypeCountDto
{
    public StudentRecommendationType Type { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class StudentRecommendationSummaryDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int DismissedCount { get; set; }
    public DateTime? LastCreatedAt { get; set; }
    public List<StudentRecommendationDto> Items { get; set; } = new();
}
