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
    /// <summary>
    /// 是否已归档隐藏。项目保存时被移除的任务不物理删除，而是标记归档，
    /// 保留历史提交与评分引用（避免“删后重建”导致任务 ID 变化、重复提交限制失效、进度超 100%）。
    /// </summary>
    public bool IsArchived { get; set; }

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
