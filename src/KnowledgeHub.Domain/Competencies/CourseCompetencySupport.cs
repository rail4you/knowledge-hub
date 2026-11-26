using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Competencies;

/// <summary>
/// 课程 → 能力 的支撑关系（(CourseId, CompetencyId) 唯一）。
/// <para>
/// <see cref="Weight"/> 表示「这门课对该能力的支撑强度」（0~1）。
/// 课程对能力的贡献度 = 课程内该能力的达成度 × 权重，避免把"只沾一点边"的课按满额算。
/// </para>
/// </summary>
public class CourseCompetencySupport : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public Guid CompetencyId { get; set; }

    /// <summary>支撑强度 0~1（0.3 弱支撑 / 0.6 中等 / 1.0 强支撑）。</summary>
    public double Weight { get; set; } = 1;

    public string? Note { get; set; }

    private CourseCompetencySupport() { }

    public CourseCompetencySupport(Guid id, Guid courseId, Guid competencyId) : base(id)
    {
        CourseId = courseId;
        CompetencyId = competencyId;
    }
}
