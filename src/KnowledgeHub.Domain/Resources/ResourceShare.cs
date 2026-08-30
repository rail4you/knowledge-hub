using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源共享记录：把源租户的资源共享给目标租户。
/// - 一个资源可以共享给多个目标租户（多条记录）。
/// - 属于源租户（TenantId = SourceTenantId），ABP 租户过滤器自动隔离。
/// - 目标租户通过查询此表判断某资源是否共享给了自己。
/// </summary>
public class ResourceShare : Entity<Guid>, IMultiTenant
{
    public Guid ResourceId { get; set; }
    public Guid SourceTenantId { get; set; }
    public Guid TargetTenantId { get; set; }
    public Guid SharedByUserId { get; set; }
    public DateTime SharedAt { get; set; }
    public string? Note { get; set; }

    public Guid? TenantId { get; set; }

    public ResourceShare()
    {
    }

    public ResourceShare(Guid id, Guid resourceId, Guid sourceTenantId, Guid targetTenantId, Guid sharedByUserId, string? note = null)
        : base(id)
    {
        ResourceId = resourceId;
        SourceTenantId = sourceTenantId;
        TargetTenantId = targetTenantId;
        SharedByUserId = sharedByUserId;
        Note = note;
        SharedAt = DateTime.UtcNow;
        TenantId = sourceTenantId;
    }
}
