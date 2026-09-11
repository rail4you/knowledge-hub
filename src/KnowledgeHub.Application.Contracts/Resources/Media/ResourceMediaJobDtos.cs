using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Media;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Application.Contracts.Resources.Media;

public class ResourceMediaJobDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public Guid? ResourceVersionId { get; set; }
    public ResourceMediaJobStatus Status { get; set; }
    public int Progress { get; set; }
    public string? ProgressMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreationTime { get; set; }
    public bool IsRead { get; set; }
    public List<ResourceArtifactDto> Artifacts { get; set; } = new();
}

public class ResourceArtifactDto
{
    public ResourceArtifactKind Kind { get; set; }
    public string Variant { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public ResourceArtifactState State { get; set; }
    public string? ErrorMessage { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class GetResourceMediaJobsInput : PagedAndSortedResultRequestDto
{
    public Guid? ResourceId { get; set; }
    public ResourceMediaJobStatus? Status { get; set; }
    /// <summary>按资源名称模糊搜索。</summary>
    public string? Filter { get; set; }
}
