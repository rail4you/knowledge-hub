using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

public class ResourceCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public Guid? TenantId { get; set; }
}
