using System;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class IndexingJobDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public Guid? ResourceVersionId { get; set; }
    public KnowledgeHub.Domain.Search.IndexingJobStatus Status { get; set; }
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public int? TotalPages { get; set; }
    public int? ProcessedPages { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>"document" | "video"</summary>
    public string JobType { get; set; } = "document";
    public int? TotalSegments { get; set; }
    public int? ProcessedSegments { get; set; }
}
