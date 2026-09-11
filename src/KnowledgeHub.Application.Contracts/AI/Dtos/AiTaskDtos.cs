using System;
using KnowledgeHub.AI;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Application.AI.Dtos;

/// <summary>
/// AI 异步生成任务 DTO。列表接口不返回 InputJson/ResultJson（大字段），详情接口返回。
/// </summary>
public class AiGenerationTaskDto
{
    public Guid Id { get; set; }
    public Guid CreatorUserId { get; set; }
    public string? CreatorUserName { get; set; }
    public AiTaskType TaskType { get; set; }
    public AiTaskStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public int Progress { get; set; }
    public string? ProgressMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>输入参数 JSON（仅详情返回）。</summary>
    public string? InputJson { get; set; }

    /// <summary>生成结果 JSON（仅详情返回；完成时才有值）。</summary>
    public string? ResultJson { get; set; }
}

public class CreateAiGenerationTaskDto
{
    public AiTaskType TaskType { get; set; }

    /// <summary>任务标题（用于列表/通知展示）。</summary>
    public string Title { get; set; } = string.Empty;

    public Guid? ResourceId { get; set; }

    /// <summary>资源名称快照（可选）。</summary>
    public string? ResourceName { get; set; }

    /// <summary>对应生成功能的输入参数 JSON（服务端按 TaskType 反序列化）。</summary>
    public string InputJson { get; set; } = string.Empty;
}

public class GetAiGenerationTaskListDto : PagedAndSortedResultRequestDto
{
    public AiTaskType? TaskType { get; set; }
    public AiTaskStatus? Status { get; set; }

    /// <summary>按标题 / 资源名模糊搜索。</summary>
    public string? Filter { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>仅看当前用户自己的任务。无任务管理权限时服务端强制为 true。</summary>
    public bool OnlyMine { get; set; }
}
