using System;
using System.Collections.Generic;
using KnowledgeHub.Domain.Search.Enums;
using KnowledgeHub.Resources;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Domain.Search;

public class DocumentIndex : FullAuditedAggregateRoot<Guid>
{
    public Guid ResourceId { get; set; }
    public int PageNumber { get; set; }
    public string PageContent { get; set; } = string.Empty;
    public string? PageTitle { get; set; }
    public string? EmbeddingVector { get; set; }
    public long? IndexingTaskId { get; set; }
    public IndexStatus IndexStatus { get; set; }
    public Guid? TenantId { get; set; }

    public virtual Resource? Resource { get; set; }
}
