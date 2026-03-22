using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using KnowledgeHub.Resources;

namespace KnowledgeHub.Domain.Search;

public class PageContent : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ResourceId { get; set; }
    public int PageNumber { get; set; }
    public float PageWidth { get; set; }
    public float PageHeight { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? TextItemsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual Resource? Resource { get; set; }
}
