using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Domain.Search;

public class VideoIndexingJob : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? ResourceVersionId { get; set; }
    public VideoIndexingJobStatus Status { get; set; } = VideoIndexingJobStatus.Pending;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public int? TotalEvents { get; set; }
    public int? ProcessedEvents { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public Guid JobId { get; set; } = Guid.NewGuid();
}

public enum VideoIndexingJobStatus : byte
{
    Pending = 0,
    Parsing = 10,
    Analyzing = 15,
    Indexing = 20,
    Completed = 30,
    Failed = 40,
    Cancelled = 50
}