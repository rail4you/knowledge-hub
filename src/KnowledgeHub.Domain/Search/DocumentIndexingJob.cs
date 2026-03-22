using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Domain.Search;

public class DocumentIndexingJob : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? ResourceVersionId { get; set; }
    public IndexingJobStatus Status { get; set; } = IndexingJobStatus.Pending;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public int? TotalPages { get; set; }
    public int? ProcessedPages { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public Guid JobId { get; set; } = Guid.NewGuid();
}

public enum IndexingJobStatus : byte
{
    Pending = 0,
    Parsing = 10,
    Indexing = 20,
    Completed = 30,
    Failed = 40,
    Cancelled = 50
}
