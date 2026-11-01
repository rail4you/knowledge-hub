using System;
using System.Threading.Tasks;
using KnowledgeHub.Cooperation;
using KnowledgeHub.Review;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Reviews;

/// <summary>
/// 合作需求公开审核落地：通过 → 发布进大厅；驳回 → 记原因。
/// </summary>
public class CooperationDemandReviewTargetHandler : IReviewTargetHandler, ITransientDependency
{
    private readonly IRepository<CooperationDemand, Guid> _demandRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<CooperationDemandReviewTargetHandler> _logger;

    public CooperationDemandReviewTargetHandler(
        IRepository<CooperationDemand, Guid> demandRepository,
        IDataFilter dataFilter,
        ILogger<CooperationDemandReviewTargetHandler> logger)
    {
        _demandRepository = demandRepository;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    public ReviewTargetType TargetType => ReviewTargetType.CooperationDemand;

    public async Task OnApprovedAsync(ReviewTicket ticket)
    {
        var demand = await FindAsync(ticket.TargetId);
        if (demand == null)
        {
            _logger.LogWarning("[CooperationReview] 需求 {Id} 不存在，跳过落地。", ticket.TargetId);
            return;
        }

        demand.MarkPublished();
        await _demandRepository.UpdateAsync(demand, autoSave: true);
    }

    public async Task OnRejectedAsync(ReviewTicket ticket)
    {
        var demand = await FindAsync(ticket.TargetId);
        if (demand == null)
        {
            return;
        }

        demand.MarkRejected(ticket.Reason);
        await _demandRepository.UpdateAsync(demand, autoSave: true);
    }

    public async Task<string?> ResolveTargetNameAsync(ReviewTicket ticket)
    {
        var demand = await FindAsync(ticket.TargetId);
        return demand?.Title;
    }

    private async Task<CooperationDemand?> FindAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _demandRepository.FindAsync(id);
        }
    }
}
