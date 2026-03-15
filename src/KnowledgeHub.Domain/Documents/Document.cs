using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Documents;

public class Document : AuditedAggregateRoot<Guid>
{
    public string Name { get; set; }

    public DocumentType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
    public Guid UserId { get; set; }
}
