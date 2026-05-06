using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighReport : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string? SummaryJson { get; set; }
    public Guid? GeneratedById { get; set; }
    public DateTime GeneratedAt { get; set; }

    public DoubleHighReport()
    {
    }

    public DoubleHighReport(Guid id, Guid projectId, string reportName)
        : base(id)
    {
        ProjectId = projectId;
        ReportName = reportName;
        GeneratedAt = DateTime.UtcNow;
    }
}
