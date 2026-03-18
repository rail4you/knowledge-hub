using System;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Resources;

public class ResourceAudit : AuditedEntity<Guid>
{
    public Guid ResourceId { get; set; }
    public AuditType AuditType { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
}
