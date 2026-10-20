using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>先修知识点关系（可跨课程）。挖掘出的边需老师确认（Status = Confirmed）才生效。</summary>
public class KpPrerequisite : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>前置知识点</summary>
    public Guid FromKpId { get; set; }

    /// <summary>后续知识点</summary>
    public Guid ToKpId { get; set; }

    public double Strength { get; set; } = 1;

    public PrerequisiteSource Source { get; set; } = PrerequisiteSource.Manual;

    public PrerequisiteStatus Status { get; set; } = PrerequisiteStatus.Proposed;

    /// <summary>展示用的触发条件描述（如「掌握率 &lt; 60%」）</summary>
    public string? Condition { get; set; }

    private KpPrerequisite() { }

    public KpPrerequisite(Guid id, Guid fromKpId, Guid toKpId) : base(id)
    {
        FromKpId = fromKpId;
        ToKpId = toKpId;
    }
}
