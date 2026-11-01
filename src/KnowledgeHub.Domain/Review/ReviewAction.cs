using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Review;

/// <summary>
/// 审核动作记录（只追加，永不更新 / 删除）。
///
/// <para>
/// 与 <see cref="ReviewTicket"/> 的关系是「聚合根 + 明细」：
/// 每通过一级、每次驳回、每次撤回都写一条，形成不可篡改的审核时间线。
/// 前端审核详情、合规审计、争议追溯都读这张表。
/// </para>
///
/// <para>
/// <b>为什么不用 <c>AuditedEntity.CreatorId</c> 代替 <see cref="ActorId"/></b>：
/// 现有 <c>ResourceAudit</c> 就是靠 <c>CreatorId</c> 记审核人，结果无法表达
/// 「代审 / 系统自动通过 / 不同阶段不同人」；这里显式建模，避免重犯。
/// </para>
/// </summary>
public class ReviewAction : CreationAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>所属审核单。</summary>
    public Guid TicketId { get; set; }

    /// <summary>动作发生时所处的阶段。</summary>
    public ReviewStage Stage { get; set; }

    /// <summary>本轮发生在审核明细上的操作（提交 / 通过 / 驳回 / 退回 / 撤回）。</summary>
    public ReviewOperation Operation { get; set; }

    /// <summary>结论（提交类/撤回类动作为空）。</summary>
    public ReviewDecision? Decision { get; set; }

    /// <summary>操作人（审核人 / 提交人）。</summary>
    public Guid ActorId { get; set; }

    /// <summary>操作人所属租户（跨校时用于区分是哪个学校的人）。</summary>
    public Guid? ActorTenantId { get; set; }

    /// <summary>操作人显示名快照。</summary>
    public string? ActorName { get; set; }

    /// <summary>意见 / 驳回原因。</summary>
    public string? Reason { get; set; }

    /// <summary>本轮轮次（与 <see cref="ReviewTicket.RoundNo"/> 对齐）。</summary>
    public int RoundNo { get; set; }

    /// <summary>
    /// 本轮提交内容快照（JSON）。审核人当时看到的是什么，就永久记什么，
    /// 避免「提交后偷偷改内容」的争议。
    /// </summary>
    public string? SnapshotJson { get; set; }

    /// <summary>审核单隶属的租户（提交方学校）。</summary>
    public Guid? TenantId { get; set; }

    public ReviewAction()
    {
    }

    public ReviewAction(
        Guid id,
        Guid ticketId,
        ReviewStage stage,
        ReviewOperation operation,
        Guid actorId,
        string? actorName)
        : base(id)
    {
        TicketId = ticketId;
        Stage = stage;
        Operation = operation;
        ActorId = actorId;
        ActorName = actorName;
    }
}
