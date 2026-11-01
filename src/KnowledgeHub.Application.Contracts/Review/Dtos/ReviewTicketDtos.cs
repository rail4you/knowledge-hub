using System;
using System.Collections.Generic;
using KnowledgeHub.Review;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Review.Dtos;

/// <summary>审核明细（时间线的一条）。</summary>
public class ReviewActionDto
{
    public Guid Id { get; set; }

    public ReviewStage Stage { get; set; }

    /// <summary>阶段显示名（院校初审 / 联盟终审）。</summary>
    public string StageName { get; set; } = string.Empty;

    public ReviewOperation Operation { get; set; }

    /// <summary>操作显示名（提交 / 通过 / 驳回 / 退回 / 撤回）。</summary>
    public string OperationName { get; set; } = string.Empty;

    public ReviewDecision? Decision { get; set; }

    public Guid ActorId { get; set; }

    public string? ActorName { get; set; }

    /// <summary>操作人所属学校（跨校场景用于区分「哪个学校的人审的」）。</summary>
    public Guid? ActorTenantId { get; set; }

    public string? Reason { get; set; }

    public int RoundNo { get; set; }

    public DateTime CreationTime { get; set; }

    /// <summary>本轮提交内容快照（审核人当时看到的内容）。</summary>
    public string? SnapshotJson { get; set; }
}

/// <summary>审核单（详情 / 列表项）。</summary>
public class ReviewTicketDto
{
    public Guid Id { get; set; }

    public ReviewTargetType TargetType { get; set; }

    /// <summary>目标类型显示名（资源库 / 内容入库 / 共建任务交付 …）。</summary>
    public string TargetTypeName { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    /// <summary>目标显示名（库名 / 资源名 …，由业务侧回填）。</summary>
    public string? TargetName { get; set; }

    public ReviewActionType ActionType { get; set; }

    /// <summary>动作显示名（新建 / 修改 / 删除 / 准入 / 发布 / 认证 / 撤销）。</summary>
    public string ActionTypeName { get; set; } = string.Empty;

    public Guid? LibraryId { get; set; }

    public string? LibraryName { get; set; }

    public Guid? ContentTenantId { get; set; }

    public string? ContentTenantName { get; set; }

    public ReviewStage Stage { get; set; }

    public string StageName { get; set; } = string.Empty;

    public int RoundNo { get; set; }

    public ReviewTicketStatus Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public Guid SubmittedBy { get; set; }

    public string? SubmittedByName { get; set; }

    public DateTime SubmittedAt { get; set; }

    public Guid? ReviewerId { get; set; }

    public string? ReviewerName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? Reason { get; set; }

    /// <summary>变更快照（前端做 diff 展示）。</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>总阶段数（1 = 单级审核，2 = 两级）。</summary>
    public int TotalStages { get; set; }

    /// <summary>当前用户是否可审批此单（已通过则恒 false）。</summary>
    public bool CanDecide { get; set; }

    /// <summary>当前用户是否可撤回（仅本人提交且仍待审）。</summary>
    public bool CanWithdraw { get; set; }

    public List<ReviewActionDto> Actions { get; set; } = new();
}

/// <summary>审核单列表查询。</summary>
public class ReviewTicketListRequestDto : PagedAndSortedResultRequestDto
{
    public ReviewTargetType? TargetType { get; set; }

    /// <summary>按阶段过滤；为空时按当前用户权限自动决定（校审看 School，联盟看 League）。</summary>
    public ReviewStage? Stage { get; set; }

    public ReviewTicketStatus? Status { get; set; }

    public Guid? LibraryId { get; set; }

    public Guid? TargetId { get; set; }

    /// <summary>只显示「本人提交」的单子。</summary>
    public bool OnlyMine { get; set; }

    public string? Filter { get; set; }
}

/// <summary>
/// 提交审核（业务服务内部调用，通常不直接暴露给前端）。
/// </summary>
public class SubmitReviewInput
{
    public ReviewTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public ReviewActionType ActionType { get; set; }

    /// <summary>库维度（跨校场景必填）。</summary>
    public Guid? LibraryId { get; set; }

    /// <summary>内容归属学校；为空时取当前租户。</summary>
    public Guid? ContentTenantId { get; set; }

    /// <summary>变更快照 JSON。</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>目标显示名（用于列表与通知，业务侧传入）。</summary>
    public string? TargetName { get; set; }
}

/// <summary>审核决策输入。</summary>
public class DecideReviewInput
{
    public Guid TicketId { get; set; }

    public ReviewDecision Decision { get; set; }

    /// <summary>驳回 / 退回时必填。</summary>
    public string? Reason { get; set; }

    /// <summary>前端持有的轮次（可选）：不一致说明已被他人处理，返回冲突。</summary>
    public int? ExpectedRoundNo { get; set; }
}
