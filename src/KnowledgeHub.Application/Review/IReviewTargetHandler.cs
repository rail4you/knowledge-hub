using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Review;

/// <summary>
/// 审核通过 / 驳回后的「落地处理器」。
///
/// <para>
/// 审核内核只负责状态与历史，<b>不懂</b>业务对象怎么改；
/// 各业务模块（资源库 / 内容入库 / 共建任务 / 企业认证）注册自己的处理器，
/// 在终审通过后按 <c>ReviewTicket.PayloadJson</c> 应用变更。
/// </para>
///
/// <para>
/// 约束：处理器必须在与审核决策<b>同一个工作单元（事务）</b>内执行，
/// 保证「审核通过」与「业务生效」不会一半成功一半失败。
/// </para>
/// </summary>
public interface IReviewTargetHandler
{
    /// <summary>本处理器负责的目标类型。</summary>
    ReviewTargetType TargetType { get; }

    /// <summary>审核全部通过后应用变更。</summary>
    Task OnApprovedAsync(ReviewTicket ticket);

    /// <summary>被驳回（可选：回滚预占、解除锁定等）。</summary>
    Task OnRejectedAsync(ReviewTicket ticket);

    /// <summary>解析目标显示名（列表/详情展示用，可为空）。</summary>
    Task<string?> ResolveTargetNameAsync(ReviewTicket ticket);
}

/// <summary>
/// 处理器分发器：按目标类型找到对应处理器。
/// </summary>
/// <summary>
/// 可选：需要感知「阶段推进」（校审通过但还没终审）的处理器实现本接口。
///
/// <para>
/// 典型用途：资源库在校审通过后应显示「待联盟终审」，而不是仍显示「待院校审核」。
/// 不实现本接口的处理器不受影响（默认不通知）。
/// </para>
/// </summary>
public interface IReviewStageHandler
{
    Task OnStageAdvancedAsync(ReviewTicket ticket);
}

public interface IReviewTargetDispatcher
{
    /// <summary>阶段推进（尚未终审通过）时通知处理器。</summary>
    Task NotifyStageAdvancedAsync(ReviewTicket ticket);

    Task ApplyApprovedAsync(ReviewTicket ticket);

    Task ApplyRejectedAsync(ReviewTicket ticket);

    Task<string?> ResolveTargetNameAsync(ReviewTicket ticket);
}

public class ReviewTargetDispatcher : IReviewTargetDispatcher, ITransientDependency
{
    private readonly IEnumerable<IReviewTargetHandler> _handlers;

    public ReviewTargetDispatcher(IEnumerable<IReviewTargetHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task NotifyStageAdvancedAsync(ReviewTicket ticket)
    {
        var handler = Find(ticket.TargetType);
        if (handler is IReviewStageHandler stageHandler)
        {
            await stageHandler.OnStageAdvancedAsync(ticket);
        }
    }

    public async Task ApplyApprovedAsync(ReviewTicket ticket)
    {
        var handler = Find(ticket.TargetType);
        if (handler != null)
        {
            await handler.OnApprovedAsync(ticket);
        }
    }

    public async Task ApplyRejectedAsync(ReviewTicket ticket)
    {
        var handler = Find(ticket.TargetType);
        if (handler != null)
        {
            await handler.OnRejectedAsync(ticket);
        }
    }

    public async Task<string?> ResolveTargetNameAsync(ReviewTicket ticket)
    {
        var handler = Find(ticket.TargetType);
        return handler == null ? null : await handler.ResolveTargetNameAsync(ticket);
    }

    private IReviewTargetHandler? Find(ReviewTargetType targetType)
    {
        return _handlers.FirstOrDefault(x => x.TargetType == targetType);
    }
}
