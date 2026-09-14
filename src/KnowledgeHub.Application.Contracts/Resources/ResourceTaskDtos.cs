using System;
using System.Collections.Generic;
using KnowledgeHub.Application.Contracts.Resources.Media;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Resources;

/// <summary>按资源聚合查询统一任务（媒体 + 文档索引 + 视频索引）。</summary>
public class GetResourceTaskGroupsInput : PagedAndSortedResultRequestDto
{
    /// <summary>按资源名称模糊搜索。</summary>
    public string? Filter { get; set; }
    /// <summary>仅查看某个资源的任务。</summary>
    public Guid? ResourceId { get; set; }
    /// <summary>按资源审核状态过滤。</summary>
    public ResourceStatus? ResourceStatus { get; set; }
    /// <summary>按任务状态过滤（归一化：pending/running/success/partial/failed/cancelled）。</summary>
    public string? TaskStatus { get; set; }
}

/// <summary>资源任务聚合组：父行为资源，子行为其下全部后台任务。</summary>
public class ResourceTaskGroupDto
{
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public Guid? TenantId { get; set; }
    public ResourceStatus ResourceStatus { get; set; }
    public ResourceMediaStatus MediaStatus { get; set; }

    /// <summary>该资源是否已对联盟审核通过/学生可见。</summary>
    public bool VisibleToStudents { get; set; }

    public int TaskCount { get; set; }
    public int FailedCount { get; set; }
    public string? LatestTaskStatus { get; set; }
    public DateTime LatestCreationTime { get; set; }

    /// <summary>该资源下的全部任务（媒体 / 文档索引 / 视频索引），按创建时间倒序。</summary>
    public List<ResourceTaskDto> Tasks { get; set; } = new();
}

/// <summary>统一的后台任务（媒体处理 / 文档索引 / 视频索引）。</summary>
public class ResourceTaskDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    /// <summary>media | document-index | video-index</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>归一化状态：pending/running/success/partial/failed/cancelled</summary>
    public string Status { get; set; } = "pending";
    public int StatusValue { get; set; }
    public int Progress { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? ResourceVersionId { get; set; }
    public bool CanRetry { get; set; }
    public List<ResourceArtifactDto> Artifacts { get; set; } = new();
}
