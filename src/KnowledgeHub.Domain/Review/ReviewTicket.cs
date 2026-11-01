using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Review;

/// <summary>
/// 统一审核单（V2.0 审核内核）。
///
/// <para>
/// 设计目标：把「教学资源库建库/改库/删库」「资源入库准入」「共建任务验收」「企业认证」
/// 这些原本各写一套的审核，收敛到同一个聚合上。好处：
/// </para>
/// <list type="number">
/// <item><description>审核历史统一、可追溯、不可覆盖（<see cref="ReviewAction"/> 只追加）；</description></item>
/// <item><description>并发与幂等只需在一处实现（<c>ConcurrencyStamp</c> + <c>PayloadHash</c>）；</description></item>
/// <item><description>待办列表、通知、统计共用一套查询。</description></item>
/// </list>
///
/// <para>
/// <b>变更快照语义</b>：<see cref="PayloadJson"/> 保存「提交了什么」。
/// 审核通过前目标实体保持原样（对外仍按旧配置服务），通过后由业务侧按快照一次性应用；
/// 因此不存在「改了一半」的中间态。
/// </para>
///
/// <para>
/// <b>租户归属</b>：<see cref="TenantId"/> 是<b>提交方</b>的租户（即内容归属学校）。
/// 联盟终审（<see cref="ReviewStage.League"/>）需要跨租户读取，查询时必须显式
/// 关闭 ABP 多租户过滤器，并按 <see cref="LibraryId"/> / 目标实体重新校验范围。
/// </para>
/// </summary>
public class ReviewTicket : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxReasonLength = 1000;
    public const int MaxPayloadHashLength = 64;
    public const int MaxTargetTypeLabelLength = 64;

    /// <summary>审核目标类型（决定审几级、谁审）。</summary>
    public ReviewTargetType TargetType { get; set; }

    /// <summary>审核目标实体 Id（LibraryId / PublicationId / DeliveryId ...）。</summary>
    public Guid TargetId { get; set; }

    /// <summary>针对目标做了什么（建/改/删/准入/发布/认证）。</summary>
    public ReviewActionType ActionType { get; set; }

    /// <summary>
    /// 库维度：跨校场景必填，决定终审方与授权范围；非库对象为空。
    /// </summary>
    public Guid? LibraryId { get; set; }

    /// <summary>
    /// 内容归属学校（= 提交方学校）。初审方由它决定，避免「别校代审」。
    /// </summary>
    public Guid? ContentTenantId { get; set; }

    /// <summary>
    /// 变更快照（JSON）。通过后由业务侧按此应用；驳回后作为「上次提交内容」留档。
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// 变更内容的稳定哈希（同目标同内容重复提交时用于幂等命中，避免生成重复待审单）。
    /// </summary>
    public string? PayloadHash { get; set; }

    /// <summary>当前所处阶段。</summary>
    public ReviewStage Stage { get; set; } = ReviewStage.School;

    /// <summary>审核轮次（每次驳回后重新提交 +1），用于审计与前端展示。</summary>
    public int RoundNo { get; set; } = 1;

    /// <summary>审核单状态。</summary>
    public ReviewTicketStatus Status { get; set; } = ReviewTicketStatus.Pending;

    /// <summary>提交人。</summary>
    public Guid SubmittedBy { get; set; }

    /// <summary>提交人显示名（快照，避免列表页 N+1）。</summary>
    public string? SubmittedByName { get; set; }

    /// <summary>提交时间。</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>最近一次审核人（通过 / 驳回时写入）。</summary>
    public Guid? ReviewerId { get; set; }

    /// <summary>最近一次审核人显示名。</summary>
    public string? ReviewerName { get; set; }

    /// <summary>最近一次审核时间。</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>最近一次审核意见 / 驳回原因。</summary>
    public string? Reason { get; set; }

    /// <summary>提交方租户（= 内容归属学校）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>审核动作历史（只追加）。</summary>
    public ICollection<ReviewAction> Actions { get; set; } = new List<ReviewAction>();

    public ReviewTicket()
    {
    }

    public ReviewTicket(
        Guid id,
        ReviewTargetType targetType,
        Guid targetId,
        ReviewActionType actionType,
        Guid submittedBy,
        string payloadJson)
        : base(id)
    {
        TargetType = targetType;
        TargetId = targetId;
        ActionType = actionType;
        SubmittedBy = submittedBy;
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        SubmittedAt = DateTime.UtcNow;
        Stage = ReviewStage.School;
        Status = ReviewTicketStatus.Pending;
        RoundNo = 1;
    }

    /// <summary>设置库维度与内容归属（跨校判定用）。</summary>
    public void SetScope(Guid? libraryId, Guid? contentTenantId)
    {
        LibraryId = libraryId;
        ContentTenantId = contentTenantId;
    }

    /// <summary>记录提交人与快照哈希。</summary>
    public void MarkSubmitted(string? submittedByName, string? payloadHash)
    {
        SubmittedByName = submittedByName;
        PayloadHash = payloadHash;
        SubmittedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 通过当前阶段：还有下一阶段则推进，否则整单通过。
    /// </summary>
    /// <param name="reviewerId">审核人。</param>
    /// <param name="reviewerName">审核人显示名。</param>
    /// <param name="reason">审核意见（可空）。</param>
    /// <param name="hasNextStage">是否还有下一阶段（由策略解析器决定）。</param>
    public void ApproveStage(Guid reviewerId, string? reviewerName, string? reason, bool hasNextStage)
    {
        ReviewerId = reviewerId;
        ReviewerName = reviewerName;
        ReviewedAt = DateTime.UtcNow;
        Reason = TrimReason(reason);

        if (hasNextStage)
        {
            Stage = NextStageOf(Stage);
            Status = ReviewTicketStatus.Pending;
            return;
        }

        Status = ReviewTicketStatus.Approved;
    }

    /// <summary>驳回（回到提交人，可修改后重提；轮次 +1 由 <see cref="Reopen"/> 处理）。</summary>
    public void Reject(Guid reviewerId, string? reviewerName, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new Volo.Abp.UserFriendlyException("驳回必须填写原因。");
        }

        ReviewerId = reviewerId;
        ReviewerName = reviewerName;
        ReviewedAt = DateTime.UtcNow;
        Reason = TrimReason(reason);
        Status = ReviewTicketStatus.Rejected;
    }

    /// <summary>提交人撤回。</summary>
    public void Cancel(string? reason = null)
    {
        if (Status != ReviewTicketStatus.Pending)
        {
            throw new Volo.Abp.UserFriendlyException("只有待审状态的审核单可以撤回。");
        }

        Status = ReviewTicketStatus.Cancelled;
        Reason = TrimReason(reason);
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 驳回后重新提交：回到第一阶段、轮次 +1、清空上一轮审核人信息。
    /// </summary>
    public void Reopen(string payloadJson, string? payloadHash)
    {
        if (Status != ReviewTicketStatus.Rejected)
        {
            throw new Volo.Abp.UserFriendlyException("只有被驳回的审核单可以重新提交。");
        }

        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        PayloadHash = payloadHash;
        RoundNo += 1;
        Stage = ReviewStage.School;
        Status = ReviewTicketStatus.Pending;
        ReviewerId = null;
        ReviewerName = null;
        ReviewedAt = null;
        Reason = null;
        SubmittedAt = DateTime.UtcNow;
    }

    /// <summary>下一阶段（目前只有两级）。</summary>
    public static ReviewStage NextStageOf(ReviewStage stage)
    {
        return stage == ReviewStage.School ? ReviewStage.League : ReviewStage.League;
    }

    /// <summary>阶段显示名（中文，供通知/日志使用）。</summary>
    public static string StageName(ReviewStage stage)
    {
        return stage == ReviewStage.School ? "院校初审" : "联盟终审";
    }

    private static string? TrimReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        return trimmed.Length > MaxReasonLength ? trimmed[..MaxReasonLength] : trimmed;
    }
}
