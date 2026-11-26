using System;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

/// <summary>
/// 学生个性化学习推荐（对齐 kg-edu 的 <c>learning_recommendation</c>）。
/// <para>
/// 与「智学分析」里 <c>RecommendationEngine</c> 的区别：那个引擎负责**抽题**（一次请求内算完就丢），
/// 这里负责**把建议落库并跟踪生命周期**（待办 → 已查看 → 进行中 → 已完成 / 已忽略），
/// 学生端才有稳定的"推荐清单"，教师端才能统计采纳率。
/// </para>
/// <para>
/// 去重键：(`StudentId`,`CourseId`,`Type`,`KnowledgeResourceId`,`ChapterId`) —— 同一件事只留一条，
/// 重复生成时更新理由与优先级，不插新行。
/// </para>
/// </summary>
public class StudentRecommendation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public StudentRecommendationType Type { get; set; }

    public StudentRecommendationStatus Status { get; set; } = StudentRecommendationStatus.Pending;

    /// <summary>优先级 1~10（越大越靠前）。</summary>
    public int Priority { get; set; } = 5;

    /// <summary>推荐理由（展示给学生，必须能解释"为什么推荐这个"）。</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>关联课程知识点（薄弱点 / 前置知识类）。</summary>
    public Guid? KnowledgeResourceId { get; set; }

    /// <summary>关联章节（未完成章节类）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>生成时的关键数值快照（如掌握度 0.42），便于事后解释。</summary>
    public double? MetricValue { get; set; }

    /// <summary>状态变更时间（完成 / 忽略）。</summary>
    public DateTime? HandledAt { get; set; }

    /// <summary>过期时间（过期的不再推送，但保留历史）。</summary>
    public DateTime? ExpiresAt { get; set; }

    private StudentRecommendation() { }

    public StudentRecommendation(Guid id, Guid studentId, Guid courseId, StudentRecommendationType type)
        : base(id)
    {
        StudentId = studentId;
        CourseId = courseId;
        Type = type;
    }

    public void Update(string reason, int priority, double? metricValue, Guid? knowledgeResourceId, Guid? chapterId)
    {
        Reason = reason;
        Priority = Math.Clamp(priority, 1, 10);
        MetricValue = metricValue;
        KnowledgeResourceId = knowledgeResourceId;
        ChapterId = chapterId;
    }

    public void SetStatus(StudentRecommendationStatus status)
    {
        Status = status;
        HandledAt = status is StudentRecommendationStatus.Completed or StudentRecommendationStatus.Dismissed
            ? DateTime.UtcNow
            : null;
    }
}
