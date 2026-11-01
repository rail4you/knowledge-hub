using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Review.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Review;

/// <summary>
/// 审核工作流（业务服务内部调用）：提交 / 决策 / 撤回 / 驳回后重提。
///
/// <para><b>事务内硬校验</b>（与设计方案 §3.1 一致）：</para>
/// <list type="number">
/// <item><description>回避本人：提交人不得审核自己提交的单。</description></item>
/// <item><description>跨阶段不同人：联盟终审人不得是该单的院校初审人。</description></item>
/// <item><description>轮次：<c>ExpectedRoundNo</c> 不符 → 冲突（防止基于旧视图审核）。</description></item>
/// <item><description>阶段权限：按 <see cref="IReviewPolicyResolver.GetStagePermission"/> 校验。</description></item>
/// <item><description>幂等：同目标 + 同动作 + 同内容哈希在「待审」集合内唯一。</description></item>
/// </list>
/// </summary>
public interface IReviewWorkflow
{
    /// <summary>提交审核（同内容重复提交返回既有待审单，不新建）。</summary>
    Task<ReviewTicket> SubmitAsync(SubmitReviewInput input);

    /// <summary>通过 / 驳回 / 退回当前阶段。</summary>
    Task<ReviewTicket> DecideAsync(DecideReviewInput input);

    /// <summary>提交人撤回。</summary>
    Task<ReviewTicket> WithdrawAsync(Guid ticketId, string? reason = null);

    /// <summary>被驳回后重新提交（轮次 +1，回到第一阶段）。</summary>
    Task<ReviewTicket> ResubmitAsync(Guid ticketId, string payloadJson, string? targetName = null);

    /// <summary>查某目标在途的待审单（没有则 null）。</summary>
    Task<ReviewTicket?> FindPendingAsync(
        ReviewTargetType targetType,
        Guid targetId,
        ReviewActionType actionType);

    /// <summary>当前用户在某阶段是否具备审批权限。</summary>
    Task<bool> CanDecideAsync(ReviewTicket ticket);
}

public class ReviewWorkflow : IReviewWorkflow, ITransientDependency
{
    private readonly IRepository<ReviewTicket, Guid> _ticketRepository;
    private readonly IRepository<ReviewAction, Guid> _actionRepository;
    private readonly IReviewPolicyResolver _policyResolver;
    private readonly IReviewTargetDispatcher _dispatcher;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ReviewWorkflow(
        IRepository<ReviewTicket, Guid> ticketRepository,
        IRepository<ReviewAction, Guid> actionRepository,
        IReviewPolicyResolver policyResolver,
        IReviewTargetDispatcher dispatcher,
        IGuidGenerator guidGenerator,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IAuthorizationService authorizationService,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _ticketRepository = ticketRepository;
        _actionRepository = actionRepository;
        _policyResolver = policyResolver;
        _dispatcher = dispatcher;
        _guidGenerator = guidGenerator;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _authorizationService = authorizationService;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<ReviewTicket> SubmitAsync(SubmitReviewInput input)
    {
        if (!_currentUser.Id.HasValue)
        {
            throw new AbpAuthorizationException("提交审核需要登录。");
        }

        if (input.TargetId == Guid.Empty)
        {
            throw new UserFriendlyException("审核目标不能为空。");
        }

        var payload = NormalizePayload(input.PayloadJson);
        var hash = ComputeHash(input, payload);
        var contentTenantId = input.ContentTenantId ?? _currentTenant.Id;

        var existing = await FindPendingAsync(input.TargetType, input.TargetId, input.ActionType);
        if (existing != null)
        {
            // 幂等：内容一致直接返回既有单；内容不同说明有人改了还没审完，明确拒绝。
            if (string.Equals(existing.PayloadHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                return existing;
            }

            throw new BusinessException("KnowledgeHub:Review:PendingChangeExists")
                .WithData("ticketId", existing.Id);
        }

        var policy = await _policyResolver.ResolveAsync(
            input.TargetType, input.ActionType, input.LibraryId, contentTenantId);

        var ticket = new ReviewTicket(
            _guidGenerator.Create(),
            input.TargetType,
            input.TargetId,
            input.ActionType,
            _currentUser.Id!.Value,
            payload)
        {
            Stage = policy.FirstStage,
            TenantId = contentTenantId
        };

        ticket.SetScope(input.LibraryId, contentTenantId);
        ticket.MarkSubmitted(CurrentActorName(), hash);

        await _ticketRepository.InsertAsync(ticket, autoSave: true);
        await AppendActionAsync(ticket, ReviewOperation.Submit, null, null);

        return ticket;
    }

    public async Task<ReviewTicket> DecideAsync(DecideReviewInput input)
    {
        var ticket = await GetTicketWithActionsAsync(input.TicketId);

        if (ticket.Status != ReviewTicketStatus.Pending)
        {
            throw new BusinessException("KnowledgeHub:Review:NotPending")
                .WithData("status", ticket.Status.ToString());
        }

        if (input.ExpectedRoundNo.HasValue && input.ExpectedRoundNo.Value != ticket.RoundNo)
        {
            throw new BusinessException("KnowledgeHub:Review:RoundConflict")
                .WithData("expected", input.ExpectedRoundNo.Value)
                .WithData("actual", ticket.RoundNo);
        }

        var actorId = _currentUser.Id ?? throw new AbpAuthorizationException("审核需要登录。");

        // ① 回避本人提交
        if (ticket.SubmittedBy == actorId)
        {
            throw new BusinessException("KnowledgeHub:Review:CannotReviewOwn")
                .WithData("ticketId", ticket.Id);
        }

        // ② 阶段权限
        if (!await CanDecideAsync(ticket))
        {
            throw new AbpAuthorizationException("当前账号无权审核该阶段的单据。");
        }

        // ③ 联盟终审人不得是院校初审人
        if (ticket.Stage == ReviewStage.League)
        {
            var schoolStageActor = ticket.Actions
                .Where(x => x.Stage == ReviewStage.School && x.Decision == ReviewDecision.Approved)
                .OrderByDescending(x => x.CreationTime)
                .FirstOrDefault()?.ActorId;

            if (schoolStageActor.HasValue && schoolStageActor.Value == actorId)
            {
                throw new BusinessException("KnowledgeHub:Review:StageActorConflict")
                    .WithData("ticketId", ticket.Id);
            }
        }

        var policy = await _policyResolver.ResolveAsync(
            ticket.TargetType, ticket.ActionType, ticket.LibraryId, ticket.ContentTenantId);

        switch (input.Decision)
        {
            case ReviewDecision.Approved:
            {
                var stageAtDecision = ticket.Stage;
                var hasNext = policy.HasNextAfter(stageAtDecision);
                ticket.ApproveStage(actorId, CurrentActorName(), input.Reason, hasNext);
                await AppendActionAsync(
                    ticket, ReviewOperation.Approve, ReviewDecision.Approved, input.Reason, stageAtDecision);
                await _ticketRepository.UpdateAsync(ticket, autoSave: true);

                if (ticket.Status == ReviewTicketStatus.Approved)
                {
                    // 终审通过：在同一事务内让业务生效（失败则整体回滚，不会出现「审过但没落地」）
                    await _dispatcher.ApplyApprovedAsync(ticket);
                }
                else
                {
                    // 仍有下一阶段：让业务侧把状态改成「待联盟终审」等中间态
                    await _dispatcher.NotifyStageAdvancedAsync(ticket);
                }

                return ticket;
            }

            case ReviewDecision.Rejected:
            case ReviewDecision.Returned:
            {
                if (string.IsNullOrWhiteSpace(input.Reason))
                {
                    throw new UserFriendlyException("驳回 / 退回必须填写原因。");
                }

                var stageAtReject = ticket.Stage;
                ticket.Reject(actorId, CurrentActorName(), input.Reason);
                await AppendActionAsync(
                    ticket,
                    input.Decision == ReviewDecision.Returned ? ReviewOperation.Return : ReviewOperation.Reject,
                    input.Decision,
                    input.Reason,
                    stageAtReject);
                await _ticketRepository.UpdateAsync(ticket, autoSave: true);

                await _dispatcher.ApplyRejectedAsync(ticket);
                return ticket;
            }

            default:
                throw new UserFriendlyException("不支持的审核结论。");
        }
    }

    public async Task<ReviewTicket> WithdrawAsync(Guid ticketId, string? reason = null)
    {
        var ticket = await GetTicketWithActionsAsync(ticketId);
        var actorId = _currentUser.Id ?? throw new AbpAuthorizationException("撤回需要登录。");

        if (ticket.SubmittedBy != actorId)
        {
            throw new BusinessException("KnowledgeHub:Review:WithdrawOnlyBySubmitter");
        }

        ticket.Cancel(reason);
        await AppendActionAsync(ticket, ReviewOperation.Withdraw, null, reason);
        await _ticketRepository.UpdateAsync(ticket, autoSave: true);

        return ticket;
    }

    public async Task<ReviewTicket> ResubmitAsync(Guid ticketId, string payloadJson, string? targetName = null)
    {
        var ticket = await GetTicketWithActionsAsync(ticketId);
        var actorId = _currentUser.Id ?? throw new AbpAuthorizationException("重新提交需要登录。");

        if (ticket.SubmittedBy != actorId)
        {
            throw new BusinessException("KnowledgeHub:Review:ResubmitOnlyBySubmitter");
        }

        var payload = NormalizePayload(payloadJson);
        var hash = ComputeHash(ticket.TargetType, ticket.TargetId, ticket.ActionType, payload);

        ticket.Reopen(payload, hash);
        await AppendActionAsync(ticket, ReviewOperation.Submit, null, null);
        await _ticketRepository.UpdateAsync(ticket, autoSave: true);

        return ticket;
    }

    public async Task<ReviewTicket?> FindPendingAsync(
        ReviewTargetType targetType,
        Guid targetId,
        ReviewActionType actionType)
    {
        // 跨租户：联盟侧提交的单可能归属别校，统一关闭过滤器后按目标精确匹配
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _ticketRepository.GetQueryableAsync();
            return await _asyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.TargetType == targetType
                                 && x.TargetId == targetId
                                 && x.ActionType == actionType
                                 && x.Status == ReviewTicketStatus.Pending)
                     .OrderByDescending(x => x.CreationTime));
        }
    }

    public async Task<bool> CanDecideAsync(ReviewTicket ticket)
    {
        var permission = _policyResolver.GetStagePermission(ticket.TargetType, ticket.Stage);
        return await _authorizationService.IsGrantedAsync(permission);
    }

    /// <summary>取单（含审核时间线）。跨租户可用：联盟终审可能审别校的单。</summary>
    private async Task<ReviewTicket> GetTicketWithActionsAsync(Guid ticketId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _ticketRepository.WithDetailsAsync(x => x.Actions);
            var ticket = await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == ticketId));
            if (ticket == null)
            {
                throw new UserFriendlyException("审核单不存在。");
            }

            return ticket;
        }
    }

    private async Task AppendActionAsync(
        ReviewTicket ticket,
        ReviewOperation operation,
        ReviewDecision? decision,
        string? reason,
        ReviewStage? stageOverride = null)
    {
        // 注意：阶段必须由调用方在「状态推进之前」捕获并传入。
        // 否则 ApproveStage 先把 Stage 推到下一级，明细就会被记到下一级，
        // 导致「联盟终审人不得是院校初审人」的校验找不到阶段一的通过记录（真实踩过）。
        var action = new ReviewAction(
            _guidGenerator.Create(),
            ticket.Id,
            stageOverride ?? ticket.Stage,
            operation,
            _currentUser.Id ?? Guid.Empty,
            CurrentActorName())
        {
            Decision = decision,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RoundNo = ticket.RoundNo,
            ActorTenantId = _currentTenant.Id,
            SnapshotJson = operation == ReviewOperation.Submit ? ticket.PayloadJson : null,
            TenantId = ticket.TenantId
        };

        await _actionRepository.InsertAsync(action, autoSave: true);
    }

    private string? CurrentActorName()
    {
        return _currentUser.Name ?? _currentUser.UserName;
    }

    private static string NormalizePayload(string? payloadJson)
    {
        return string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
    }

    /// <summary>内容哈希：目标 + 动作 + 负载，用于待审单幂等去重。</summary>
    private static string ComputeHash(SubmitReviewInput input, string payload)
    {
        return ComputeHash(input.TargetType, input.TargetId, input.ActionType, payload);
    }

    private static string ComputeHash(
        ReviewTargetType targetType,
        Guid targetId,
        ReviewActionType actionType,
        string payload)
    {
        var raw = $"{(byte)targetType}|{targetId:N}|{(byte)actionType}|{payload}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
