using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighIndicatorValue : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid IndicatorId { get; set; }
    public decimal Value { get; set; }

    /// <summary>
    /// 文字填报内容（填报类型为「填写说明」的指标使用）。
    /// </summary>
    public string? TextValue { get; set; }

    public string? Note { get; set; }
    public DoubleHighValueSourceType SourceType { get; set; } = DoubleHighValueSourceType.Manual;
    public DateTime CollectedAt { get; set; }

    public DoubleHighIndicatorValue()
    {
    }

    public DoubleHighIndicatorValue(Guid id, Guid projectId, Guid indicatorId, decimal value)
        : base(id)
    {
        ProjectId = projectId;
        IndicatorId = indicatorId;
        Value = value;
        CollectedAt = DateTime.UtcNow;
    }
}
