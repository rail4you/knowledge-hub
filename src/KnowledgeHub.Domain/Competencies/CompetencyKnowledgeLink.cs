using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Competencies;

/// <summary>
/// 能力 ← 课程知识点 的支撑关系（(CompetencyId, KnowledgeResourceId) 唯一）。
/// <para>
/// 达成度的计算入口：能力达成度 = Σ(知识点掌握度 × 权重) / Σ(权重)。
/// 只统计「有作答证据」的知识点，证据不足时不判薄弱（与学情口径一致）。
/// </para>
/// </summary>
public class CompetencyKnowledgeLink : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CompetencyId { get; set; }

    public Guid KnowledgeResourceId { get; set; }

    /// <summary>该知识点对该能力的支撑权重（默认 1，同一能力下权重和不必为 1）。</summary>
    public double Weight { get; set; } = 1;

    private CompetencyKnowledgeLink() { }

    public CompetencyKnowledgeLink(Guid id, Guid competencyId, Guid knowledgeResourceId) : base(id)
    {
        CompetencyId = competencyId;
        KnowledgeResourceId = knowledgeResourceId;
    }
}
