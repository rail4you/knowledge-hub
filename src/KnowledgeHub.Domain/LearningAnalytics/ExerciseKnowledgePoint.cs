using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 习题 ↔ 知识点映射（多对多带权重，同一题各知识点权重和为 1）。
/// 使用代理主键 + (ExerciseId, KnowledgePointId) 唯一索引，便于仓储与增量维护。
/// </summary>
public class ExerciseKnowledgePoint : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ExerciseId { get; set; }

    public Guid KnowledgePointId { get; set; }

    /// <summary>该知识点在这道题中的权重（0~1）</summary>
    public double Weight { get; set; } = 1;

    public KpMapSource Source { get; set; } = KpMapSource.Seed;

    private ExerciseKnowledgePoint() { }

    public ExerciseKnowledgePoint(Guid id, Guid exerciseId, Guid knowledgePointId) : base(id)
    {
        ExerciseId = exerciseId;
        KnowledgePointId = knowledgePointId;
    }
}
