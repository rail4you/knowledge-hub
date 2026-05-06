using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighIndicator : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string IndicatorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public DoubleHighDataSourceType DataSourceType { get; set; } = DoubleHighDataSourceType.Manual;
    public decimal? TargetValue { get; set; }
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }

    public DoubleHighIndicator()
    {
    }

    public DoubleHighIndicator(Guid id, Guid projectId, string indicatorCode, string name)
        : base(id)
    {
        ProjectId = projectId;
        IndicatorCode = indicatorCode;
        Name = name;
    }
}
