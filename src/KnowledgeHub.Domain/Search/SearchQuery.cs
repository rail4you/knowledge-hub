using System;
using KnowledgeHub.Domain.Search.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Domain.Search;

public class SearchQuery : FullAuditedAggregateRoot<Guid>
{
    public Guid UserId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public SearchType SearchType { get; set; }
    public int ResultCount { get; set; }
    public string? Filters { get; set; }
    public Guid? TenantId { get; set; }
}
