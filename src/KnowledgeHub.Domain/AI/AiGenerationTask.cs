using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.AI;

/// <summary>
/// AI 异步生成任务（教案 / 案例分析 / 职业规划 / 习题）。
/// 任务提交后立即落库，由后台作业执行；前端可切页、轮询进度、查看结果。
/// </summary>
public class AiGenerationTask : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>任务发起人（用于"只看自己的任务"与通知归属）。</summary>
    public Guid CreatorUserId { get; set; }

    public AiTaskType TaskType { get; set; }

    public AiTaskStatus Status { get; set; } = AiTaskStatus.Pending;

    /// <summary>人类可读标题，如"《数据结构》第3章教案"。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>关联资源（可选，便于结果表展示来源）。</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>资源名称快照（避免跨租户/已删除资源查不到）。</summary>
    public string? ResourceName { get; set; }

    /// <summary>0-100 进度。</summary>
    public int Progress { get; set; }

    /// <summary>当前步骤提示，如"正在生成第 2/5 章…"。</summary>
    public string? ProgressMessage { get; set; }

    /// <summary>输入参数 JSON（用于重试与结果表回显）。</summary>
    public string? InputJson { get; set; }

    /// <summary>生成结果 JSON（完成后写入）。</summary>
    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int RetryCount { get; set; }

    /// <summary>发起人是否已读该完成通知（驱动顶部铃铛未读数）。</summary>
    public bool IsRead { get; set; }

    protected AiGenerationTask()
    {
    }

    public AiGenerationTask(Guid id, Guid creatorUserId, AiTaskType taskType, string title)
        : base(id)
    {
        CreatorUserId = creatorUserId;
        TaskType = taskType;
        Title = title;
    }
}

public enum AiTaskType : byte
{
    LessonPlanSingle = 0,
    LessonPlanMulti = 10,
    CaseAnalysis = 20,
    CareerGuidance = 30,
    ExerciseGenerate = 40
}

public enum AiTaskStatus : byte
{
    Pending = 0,
    Running = 10,
    Completed = 30,
    Failed = 40,
    Cancelled = 50
}
