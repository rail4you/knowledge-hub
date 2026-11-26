using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

/// <summary>
/// 岗位核心工作任务（对齐 kg-edu 的 <c>job_core_task</c>）。
/// <para>
/// 岗位能力不是直接列的，而是从「这个岗位实际要干的几件事」拆出来的 ——
/// 这样能力要求才有业务依据，也便于和企业的岗位说明书对齐。
/// </para>
/// </summary>
public class JobCoreTask : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid GraphId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>任务权重（0~1）：表示这项任务在岗位工作中的占比。</summary>
    public double Weight { get; set; } = 1;

    public int SortOrder { get; set; }

    private JobCoreTask() { }

    public JobCoreTask(Guid id, Guid graphId, string title) : base(id)
    {
        GraphId = graphId;
        Title = title;
    }
}
