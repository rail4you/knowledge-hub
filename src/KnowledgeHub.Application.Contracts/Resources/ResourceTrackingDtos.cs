using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Resources;

/// <summary>资源跟踪列表查询（左侧资源列表）。</summary>
public class GetTrackingResourcesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public ResourceStatus? Status { get; set; }
}

/// <summary>资源跟踪列表项。</summary>
public class ResourceTrackingResourceDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
    public string? FileExtension { get; set; }
    public ResourceStatus Status { get; set; }
    public ResourceMediaStatus MediaStatus { get; set; }
    public int CurrentVersion { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
}

/// <summary>资源全链路时间轴：上传 → 预览 → 院校审核 → 索引 → 联盟审核 → 学生可见。</summary>
public class ResourceTrackingTimelineDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
    public string? FileExtension { get; set; }
    public ResourceStatus Status { get; set; }
    public ResourceMediaStatus MediaStatus { get; set; }
    public int CurrentVersion { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool VisibleToStudents { get; set; }
    public DateTime CreationTime { get; set; }
    public List<ResourceTrackingStepDto> Steps { get; set; } = new();
}

/// <summary>时间轴上的一个阶段，内含该阶段的独立任务（可分别重试）。</summary>
public class ResourceTrackingStepDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>pending | running | success | failed | skipped</summary>
    public string Status { get; set; } = "pending";
    public DateTime? Time { get; set; }
    public int? Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ResourceTrackingTaskDto> Tasks { get; set; } = new();
}

/// <summary>单个后台任务（媒体处理 / 文档索引 / 视频索引）。</summary>
public class ResourceTrackingTaskDto
{
    public Guid Id { get; set; }
    /// <summary>media | document-index | video-index</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>pending | running | success | partial | failed | cancelled</summary>
    public string Status { get; set; } = "pending";
    public int Progress { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? ResourceVersionId { get; set; }
    public bool CanRetry { get; set; }
    public List<ResourceTrackingArtifactDto> Artifacts { get; set; } = new();
}

public class ResourceTrackingArtifactDto
{
    /// <summary>thumbnail | preview-pdf</summary>
    public string Kind { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    /// <summary>ready | failed</summary>
    public string State { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class RetryTrackingTaskInput
{
    public Guid ResourceId { get; set; }
    /// <summary>media | document-index | video-index</summary>
    public string Kind { get; set; } = string.Empty;
    public Guid TaskId { get; set; }
}
