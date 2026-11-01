using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Enterprises;
using KnowledgeHub.Review;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.Reviews;

/// <summary>
/// 企业认证的审核落地（<see cref="ReviewTargetType.EnterpriseProfile"/>）。
/// 通过 → 认证状态置「已认证」；驳回 → 「已驳回」并记录原因。
/// </summary>
public class EnterpriseProfileReviewTargetHandler : IReviewTargetHandler, ITransientDependency
{
    private readonly IRepository<EnterpriseProfile, Guid> _profileRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<EnterpriseProfileReviewTargetHandler> _logger;

    public EnterpriseProfileReviewTargetHandler(
        IRepository<EnterpriseProfile, Guid> profileRepository,
        IDataFilter dataFilter,
        ICurrentUser currentUser,
        ILogger<EnterpriseProfileReviewTargetHandler> logger)
    {
        _profileRepository = profileRepository;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
        _logger = logger;
    }

    public ReviewTargetType TargetType => ReviewTargetType.EnterpriseProfile;

    public async Task OnApprovedAsync(ReviewTicket ticket)
    {
        var profile = await FindAsync(ticket.TargetId);
        if (profile == null)
        {
            _logger.LogWarning("[EnterpriseReview] 企业 {Id} 不存在，跳过落地。", ticket.TargetId);
            return;
        }

        profile.MarkCertified(_currentUser.Id);
        await _profileRepository.UpdateAsync(profile, autoSave: true);
    }

    public async Task OnRejectedAsync(ReviewTicket ticket)
    {
        var profile = await FindAsync(ticket.TargetId);
        if (profile == null)
        {
            return;
        }

        profile.MarkRejected(ticket.Reason);
        await _profileRepository.UpdateAsync(profile, autoSave: true);
    }

    public async Task<string?> ResolveTargetNameAsync(ReviewTicket ticket)
    {
        var profile = await FindAsync(ticket.TargetId);
        return profile?.Name;
    }

    private async Task<EnterpriseProfile?> FindAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _profileRepository.FindAsync(id);
        }
    }
}
