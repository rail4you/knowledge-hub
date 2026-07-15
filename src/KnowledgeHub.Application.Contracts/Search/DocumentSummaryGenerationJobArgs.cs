using System;

namespace KnowledgeHub.Application.Contracts.Search;

public class DocumentSummaryGenerationJobArgs
{
    public Guid ResourceId { get; set; }
    public Guid? TenantId { get; set; }
}