using System;
using KnowledgeHub.Resources;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Domain.Search;

public class ResourcePageIndex : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid ResourceVersionId { get; set; }
    public string PageIndexJson { get; set; } = string.Empty;
    public string? SourceFormat { get; set; }
    public string? Model { get; set; }
    public int NodeCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Resource? Resource { get; set; }
    public ResourceVersion? ResourceVersion { get; set; }
}
