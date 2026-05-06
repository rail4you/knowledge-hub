using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumTask : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Requirement { get; set; }
    public DateTime? DueTime { get; set; }
    public decimal ScoreWeight { get; set; }
    public int SortOrder { get; set; }

    public PracticumTask()
    {
    }

    public PracticumTask(Guid id, Guid projectId, string title)
        : base(id)
    {
        ProjectId = projectId;
        Title = title;
    }
}
