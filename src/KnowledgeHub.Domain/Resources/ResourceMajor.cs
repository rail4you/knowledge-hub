using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源—专业关联（一个资源可归属多个专业）。
/// 其中一条为“主专业”（<see cref="IsPrimary"/>），与 <see cref="Resource.MajorId"/> 双写保持一致，
/// 历史查询/统计只认主专业；筛选/搜索认全量。无任何关联时视为“无专业归属”。
/// </summary>
public class ResourceMajor : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid MajorId { get; set; }
    public bool IsPrimary { get; set; }

    public ResourceMajor()
    {
    }

    public ResourceMajor(Guid id, Guid resourceId, Guid majorId, bool isPrimary = false)
        : base(id)
    {
        ResourceId = resourceId;
        MajorId = majorId;
        IsPrimary = isPrimary;
    }
}
