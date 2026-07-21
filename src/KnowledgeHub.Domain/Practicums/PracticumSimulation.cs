using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumSimulation : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string EntryPath { get; set; } = "index.html";
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public PracticumSimulationStatus Status { get; set; }
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public bool CoopCoepRequired { get; set; } = true;
    public int SortOrder { get; set; }

    public PracticumSimulation()
    {
    }

    public PracticumSimulation(Guid id, Guid projectId, string name, string slug)
        : base(id)
    {
        ProjectId = projectId;
        Name = name;
        Slug = slug;
    }
}
