using System;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Documents;

public class DocumentDto : AuditedEntityDto<Guid>
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Name { get; set; }

    public DocumentType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
}
