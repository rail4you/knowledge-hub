using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 课程知识点。归一化后 <see cref="CanonicalId"/> 指向标准知识点（自身为空表示标准点）。
/// </summary>
public class KnowledgePoint : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>所属章节（用于习题自动映射与教学达成度聚合）</summary>
    public Guid? ChapterId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>被合并后指向标准知识点</summary>
    public Guid? CanonicalId { get; set; }

    /// <summary>教学大纲顺序</summary>
    public int SyllabusOrder { get; set; }

    /// <summary>语义向量（scalar 向量检索用，可为空）</summary>
    public float[]? Embedding { get; set; }

    public KnowledgePointStatus Status { get; set; } = KnowledgePointStatus.Confirmed;

    public KpMapSource Source { get; set; } = KpMapSource.Seed;

    private KnowledgePoint() { }

    public KnowledgePoint(Guid id, Guid courseId, string name) : base(id)
    {
        CourseId = courseId;
        Name = name;
    }
}
