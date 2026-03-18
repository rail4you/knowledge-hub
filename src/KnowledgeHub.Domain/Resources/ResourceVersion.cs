using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Resources;

public class ResourceVersion : AuditedEntity<Guid>
{
    public Guid ResourceId { get; set; }
    public int Version { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? UpdateContent { get; set; }
    public bool IsCurrentVersion { get; set; }
}
