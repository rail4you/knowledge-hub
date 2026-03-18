using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Resources;

public class ResourceCollection : AuditedEntity<Guid>
{
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
}
