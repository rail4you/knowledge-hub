using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Review.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace KnowledgeHub.Review;

/// <summary>
/// 审核中心（HTTP 层）：列表 / 详情 / 决策 / 撤回。
///
/// <para>
/// 权限模型：本服务<b>不</b>在类级别加 <c>[Authorize]</c>（校审、联盟审用的是两组不同权限，
/// 且同一用户可能只有其中一组）。改为：
/// </para>
/// <list type="bullet">
/// <item><description>列表：按当前用户实际持有的阶段权限过滤（校审只看 School，联盟只看 League）。</description></item>
/// <item><description>决策：在 <see cref="IReviewWorkflow.DecideAsync"/> 内按阶段权限强校验。</description></item>
/// <item><description>撤回：仅提交人本人。</description></item>
/// </list>
/// </summary>
[IgnoreAntiforgeryToken]
public class ReviewTicketAppService : KnowledgeHubAppService, IReviewTicketAppService
{
    private readonly IRepository<ReviewTicket, Guid> _ticketRepository;
    private readonly IReviewWorkflow _workflow;
    private readonly IReviewPolicyResolver _policyResolver;
    private readonly IReviewTargetDispatcher _dispatcher;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ReviewTicketAppService(
        IRepository<ReviewTicket, Guid> ticketRepository,
        IReviewWorkflow workflow,
        IReviewPolicyResolver policyResolver,
        IReviewTargetDispatcher dispatcher,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _ticketRepository = ticketRepository;
        _workflow = workflow;
        _policyResolver = policyResolver;
        _dispatcher = dispatcher;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<PagedResultDto<ReviewTicketDto>> GetListAsync(ReviewTicketListRequestDto input)
    {
        var hasSchoolAudit = await AuthorizationService.IsGrantedAsync(
            KnowledgeHubPermissions.ResourceLibraries.Audit);
        var hasLeagueAudit = await AuthorizationService.IsGrantedAsync(
            KnowledgeHubPermissions.ResourceLibraries.LeagueAudit);
        var hasCooperationReview = await AuthorizationService.IsGrantedAsync(
            KnowledgeHubPermissions.Cooperation.Review);
        var hasJobReview = await AuthorizationService.IsGrantedAsync(
            KnowledgeHubPermissions.Employment.ReviewJob);

        if (!hasSchoolAudit && !hasLeagueAudit && !hasCooperationReview && !hasJobReview)
        {
            throw new AbpAuthorizationException("当前账号没有审核权限。");
        }

        // 联盟终审必然跨租户：权限为 Host-only，持有时说明在宿主上下文，
        // 必须关闭租户过滤器才能看到各校提交的单（否则只能看到 TenantId 为空的孤儿行）。
        var crossTenant = hasLeagueAudit;

        using (crossTenant ? _dataFilter.Disable<IMultiTenant>() : null)
        {
            var query = await _ticketRepository.WithDetailsAsync(x => x.Actions);

            if (input.TargetId.HasValue)
            {
                query = query.Where(x => x.TargetId == input.TargetId.Value);
            }

            if (input.TargetType.HasValue)
            {
                query = query.Where(x => x.TargetType == input.TargetType.Value);
            }

            if (input.LibraryId.HasValue)
            {
                query = query.Where(x => x.LibraryId == input.LibraryId.Value);
            }

            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            // 阶段过滤：显式传参优先；否则按权限自动收敛，
            // 避免「校审看到联盟待审的单、点了却 403」。
            if (input.Stage.HasValue)
            {
                query = query.Where(x => x.Stage == input.Stage.Value);
            }
            else if (hasSchoolAudit && !hasLeagueAudit)
            {
                query = query.Where(x => x.Stage == ReviewStage.School);
            }
            else if (hasLeagueAudit && !hasSchoolAudit)
            {
                query = query.Where(x => x.Stage == ReviewStage.League);
            }

            if (input.OnlyMine)
            {
                var userId = CurrentUser.Id ?? Guid.Empty;
                query = query.Where(x => x.SubmittedBy == userId);
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var keyword = input.Filter.Trim();
                query = query.Where(x => x.TargetId.ToString().Contains(keyword));
            }

            var totalCount = await _asyncExecuter.CountAsync(query);

            query = query
                .OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount);

            var tickets = await _asyncExecuter.ToListAsync(query);
            var dtos = new List<ReviewTicketDto>(tickets.Count);

            foreach (var ticket in tickets)
            {
                dtos.Add(await ToDtoAsync(ticket, resolvedNames: null));
            }

            return new PagedResultDto<ReviewTicketDto>(totalCount, dtos);
        }
    }

    public async Task<ReviewTicketDto> GetAsync(Guid id)
    {
        ReviewTicket ticket;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _ticketRepository.WithDetailsAsync(x => x.Actions);
            ticket = await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
                     ?? throw new UserFriendlyException("审核单不存在或无权访问。");
        }

        await EnsureCanViewAsync(ticket);
        return await ToDtoAsync(ticket, resolvedNames: null);
    }

    public async Task<ReviewTicketDto> DecideAsync(DecideReviewInput input)
    {
        var ticket = await _workflow.DecideAsync(input);

        // 决策后重新取（含新写入的 action）并回填展示信息
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _ticketRepository.WithDetailsAsync(x => x.Actions);
            ticket = await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == input.TicketId)) ?? ticket;
        }

        return await ToDtoAsync(ticket, resolvedNames: null);
    }

    public async Task<ReviewTicketDto> WithdrawAsync(Guid id, string? reason = null)
    {
        var ticket = await _workflow.WithdrawAsync(id, reason);
        return await ToDtoAsync(ticket, resolvedNames: null);
    }

    public async Task<ReviewTicketDto> ResubmitAsync(Guid id, string? payloadJson = null)
    {
        ReviewTicket current;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            current = await _ticketRepository.FindAsync(id)
                      ?? throw new UserFriendlyException("审核单不存在。");
        }

        var payload = string.IsNullOrWhiteSpace(payloadJson) ? current.PayloadJson : payloadJson;
        var ticket = await _workflow.ResubmitAsync(id, payload);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _ticketRepository.WithDetailsAsync(x => x.Actions);
            ticket = await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id)) ?? ticket;
        }

        return await ToDtoAsync(ticket, resolvedNames: null);
    }

    public async Task<List<ReviewTicketDto>> GetByTargetAsync(ReviewTargetType targetType, Guid targetId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _ticketRepository.WithDetailsAsync(x => x.Actions);
            var tickets = await _asyncExecuter.ToListAsync(
                query.Where(x => x.TargetType == targetType && x.TargetId == targetId)
                     .OrderByDescending(x => x.CreationTime));

            var result = new List<ReviewTicketDto>(tickets.Count);
            foreach (var ticket in tickets)
            {
                result.Add(await ToDtoAsync(ticket, resolvedNames: null));
            }

            return result;
        }
    }

    public async Task<int> GetMyPendingCountAsync()
    {
        var hasSchoolAudit = await AuthorizationService.IsGrantedAsync(
            KnowledgeHubPermissions.ResourceLibraries.Audit);
        var hasLeagueAudit = await AuthorizationService.IsGrantedAsync(
            KnowledgeHubPermissions.ResourceLibraries.LeagueAudit);

        if (!hasSchoolAudit && !hasLeagueAudit)
        {
            return 0;
        }

        using (hasLeagueAudit ? _dataFilter.Disable<IMultiTenant>() : null)
        {
            var query = await _ticketRepository.GetQueryableAsync();
            query = query.Where(x => x.Status == ReviewTicketStatus.Pending);

            if (hasSchoolAudit && !hasLeagueAudit)
            {
                query = query.Where(x => x.Stage == ReviewStage.School);
            }
            else if (hasLeagueAudit && !hasSchoolAudit)
            {
                query = query.Where(x => x.Stage == ReviewStage.League);
            }

            return await _asyncExecuter.CountAsync(query);
        }
    }

    /// <summary>详情查看权限：提交人 / 本校管理员 / 联盟审核员。</summary>
    private async Task EnsureCanViewAsync(ReviewTicket ticket)
    {
        var userId = CurrentUser.Id;
        if (userId.HasValue && ticket.SubmittedBy == userId.Value)
        {
            return;
        }

        if (CurrentTenant.Id.HasValue && ticket.ContentTenantId == CurrentTenant.Id.Value)
        {
            return;
        }

        if (await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.ResourceLibraries.LeagueAudit) ||
            await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.ResourceLibraries.Audit) ||
            await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Cooperation.Review))
        {
            return;
        }

        throw new AbpAuthorizationException("无权查看该审核单。");
    }

    private async Task<ReviewTicketDto> ToDtoAsync(ReviewTicket ticket, Dictionary<Guid, string>? resolvedNames)
    {
        var targetName = await _dispatcher.ResolveTargetNameAsync(ticket);

        var stageIds = await ResolveStagesAsync(ticket);

        var dto = new ReviewTicketDto
        {
            Id = ticket.Id,
            TargetType = ticket.TargetType,
            TargetTypeName = TargetTypeName(ticket.TargetType),
            TargetId = ticket.TargetId,
            TargetName = targetName,
            ActionType = ticket.ActionType,
            ActionTypeName = ActionTypeName(ticket.ActionType),
            LibraryId = ticket.LibraryId,
            ContentTenantId = ticket.ContentTenantId,
            Stage = ticket.Stage,
            StageName = ReviewTicket.StageName(ticket.Stage),
            RoundNo = ticket.RoundNo,
            Status = ticket.Status,
            StatusName = StatusName(ticket.Status),
            SubmittedBy = ticket.SubmittedBy,
            SubmittedByName = ticket.SubmittedByName,
            SubmittedAt = ticket.SubmittedAt,
            ReviewerId = ticket.ReviewerId,
            ReviewerName = ticket.ReviewerName,
            ReviewedAt = ticket.ReviewedAt,
            Reason = ticket.Reason,
            PayloadJson = ticket.PayloadJson,
            TotalStages = stageIds.Count,
            CanDecide = ticket.Status == ReviewTicketStatus.Pending
                        && CurrentUser.Id.HasValue
                        && ticket.SubmittedBy != CurrentUser.Id.Value
                        && await _workflow.CanDecideAsync(ticket),
            CanWithdraw = ticket.Status == ReviewTicketStatus.Pending
                          && CurrentUser.Id.HasValue
                          && ticket.SubmittedBy == CurrentUser.Id.Value,
            Actions = ticket.Actions
                .OrderBy(x => x.CreationTime)
                .Select(x => new ReviewActionDto
                {
                    Id = x.Id,
                    Stage = x.Stage,
                    StageName = ReviewTicket.StageName(x.Stage),
                    Operation = x.Operation,
                    OperationName = OperationName(x.Operation),
                    Decision = x.Decision,
                    ActorId = x.ActorId,
                    ActorName = x.ActorName,
                    ActorTenantId = x.ActorTenantId,
                    Reason = x.Reason,
                    RoundNo = x.RoundNo,
                    CreationTime = x.CreationTime,
                    SnapshotJson = x.SnapshotJson
                })
                .ToList()
        };

        return dto;
    }

    private async Task<IReadOnlyList<ReviewStage>> ResolveStagesAsync(ReviewTicket ticket)
    {
        var policy = await _policyResolver.ResolveAsync(
            ticket.TargetType, ticket.ActionType, ticket.LibraryId, ticket.ContentTenantId);
        return policy.Stages;
    }

    private static string TargetTypeName(ReviewTargetType type)
    {
        return type switch
        {
            ReviewTargetType.Library => "教学资源库",
            ReviewTargetType.LibraryPublication => "库内内容准入",
            ReviewTargetType.BuildTaskDelivery => "共建任务交付",
            ReviewTargetType.EnterpriseProfile => "企业认证",
            ReviewTargetType.EmploymentJob => "招聘岗位",
            ReviewTargetType.CooperationDemand => "合作需求",
            _ => type.ToString()
        };
    }

    private static string ActionTypeName(ReviewActionType type)
    {
        return type switch
        {
            ReviewActionType.Create => "新建",
            ReviewActionType.Update => "修改",
            ReviewActionType.Delete => "删除（归档）",
            ReviewActionType.Admit => "准入",
            ReviewActionType.Publish => "发布",
            ReviewActionType.Certify => "认证",
            ReviewActionType.Revoke => "撤销",
            _ => type.ToString()
        };
    }

    private static string StatusName(ReviewTicketStatus status)
    {
        return status switch
        {
            ReviewTicketStatus.Pending => "待审核",
            ReviewTicketStatus.Approved => "已通过",
            ReviewTicketStatus.Rejected => "已驳回",
            ReviewTicketStatus.Cancelled => "已撤回",
            _ => status.ToString()
        };
    }

    private static string OperationName(ReviewOperation operation)
    {
        return operation switch
        {
            ReviewOperation.Submit => "提交审核",
            ReviewOperation.Approve => "审核通过",
            ReviewOperation.Reject => "驳回",
            ReviewOperation.Return => "退回补充",
            ReviewOperation.Withdraw => "撤回",
            _ => operation.ToString()
        };
    }
}
