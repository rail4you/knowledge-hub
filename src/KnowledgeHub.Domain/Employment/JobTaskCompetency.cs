using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

/// <summary>
/// 核心任务 → 所需能力（(CoreTaskId, CompetencyId) 唯一）。
/// <para>
/// 能力项复用 <c>Competencies.Competency</c>；它已经能通过
/// <c>CompetencyKnowledgeLink</c> 连到课程知识点，所以岗位图谱天然能算达成度。
/// </para>
/// </summary>
public class JobTaskCompetency : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CoreTaskId { get; set; }

    public Guid CompetencyId { get; set; }

    /// <summary>该任务对该能力的要求强度（复用支撑权重口径 0~1）。</summary>
    public double Weight { get; set; } = 1;

    /// <summary>要求等级：0 了解 / 1 熟练 / 2 精通。</summary>
    public int RequiredLevel { get; set; } = 1;

    private JobTaskCompetency() { }

    public JobTaskCompetency(Guid id, Guid coreTaskId, Guid competencyId) : base(id)
    {
        CoreTaskId = coreTaskId;
        CompetencyId = competencyId;
    }
}
