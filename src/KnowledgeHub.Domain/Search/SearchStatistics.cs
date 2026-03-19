using System;
using Volo.Abp.Domain.Entities;

namespace KnowledgeHub.Domain.Search;

public class SearchStatistics : Entity<Guid>
{
    public Guid OrganizationId { get; set; }
    public DateTime Date { get; set; }
    public int TotalSearches { get; set; }
    public int UniqueUsers { get; set; }
    public decimal AvgResultCount { get; set; }
    public string? TopSearchTerm { get; set; }
    public Guid? TenantId { get; set; }
}
