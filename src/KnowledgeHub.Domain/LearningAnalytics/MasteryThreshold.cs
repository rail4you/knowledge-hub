using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 掌握度判定线。取值顺序：知识点覆盖（<see cref="KnowledgePointId"/> 非空）
/// &gt; 课程级（<see cref="KnowledgePointId"/> 为空）&gt; 系统默认（0.60 / 0.75）。
/// </summary>
public class MasteryThreshold : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? KnowledgePointId { get; set; }

    /// <summary>薄弱线（低于此值判为薄弱）</summary>
    public double WeakLine { get; set; } = 0.60;

    /// <summary>达标线</summary>
    public double OkLine { get; set; } = 0.75;

    private MasteryThreshold() { }

    public MasteryThreshold(Guid id, Guid courseId) : base(id)
    {
        CourseId = courseId;
    }
}
