using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

public class Resource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
    public ResourceStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? TenantId { get; set; }

    public ICollection<ResourceVersion> Versions { get; set; }
    public ICollection<ResourceAudit> Audits { get; set; }
    public ICollection<ResourceCollection> Collections { get; set; }

    public Resource()
    {
        Versions = new List<ResourceVersion>();
        Audits = new List<ResourceAudit>();
        Collections = new List<ResourceCollection>();
    }
}
