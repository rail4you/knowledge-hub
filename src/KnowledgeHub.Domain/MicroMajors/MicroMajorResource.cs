using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MicroMajors;

/// <summary>
/// Bridge entity linking MicroMajors to Resources (素材)
/// </summary>
public class MicroMajorResource : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid MicroMajorId { get; set; }
    public Guid ResourceId { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }

    public MicroMajorResource()
    {
    }

    public MicroMajorResource(Guid id, Guid microMajorId, Guid resourceId, int sortOrder)
        : base(id)
    {
        MicroMajorId = microMajorId;
        ResourceId = resourceId;
        SortOrder = sortOrder;
    }
}
