using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighProject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighProjectStatus Status { get; set; } = DoubleHighProjectStatus.Draft;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? LastCollectedAt { get; set; }

    public DoubleHighProject()
    {
    }

    public DoubleHighProject(Guid id, string title, string batchCode)
        : base(id)
    {
        Title = title;
        BatchCode = batchCode;
    }
}
