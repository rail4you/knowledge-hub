using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源媒体处理任务（后台 ETL）：为某个资源版本生成缩略图 / 预览材料等衍生文件。
/// 与索引任务相互独立；上传/换版本时入队，前端只消费生成物（未就绪则展示处理中）。
/// </summary>
public class ResourceMediaJob : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ResourceId { get; set; }

    /// <summary>处理针对的资源版本（生成物按版本隔离，换版本不串味）。</summary>
    public Guid? ResourceVersionId { get; set; }

    public ResourceMediaJobStatus Status { get; set; } = ResourceMediaJobStatus.Pending;

    /// <summary>0-100 进度。</summary>
    public int Progress { get; set; }

    /// <summary>当前步骤提示，如"正在生成缩略图…"。</summary>
    public string? ProgressMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? NextRetryAt { get; set; }

    protected ResourceMediaJob()
    {
    }

    public ResourceMediaJob(Guid id, Guid resourceId, Guid? resourceVersionId)
        : base(id)
    {
        ResourceId = resourceId;
        ResourceVersionId = resourceVersionId;
    }
}

public enum ResourceMediaJobStatus : byte
{
    Pending = 0,
    Running = 10,
    Completed = 30,
    /// <summary>部分生成物失败（其余可正常使用）。</summary>
    PartialFailed = 35,
    Failed = 40,
    Cancelled = 50
}
