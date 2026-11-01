using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Review;

/// <summary>
/// 审核策略：某个「目标 + 动作」需要走哪几个阶段、每个阶段由谁（哪个权限）审。
///
/// <para>
/// 与需求方确认的口径（两角色、不新增"库负责人"）：
/// </para>
/// <list type="bullet">
/// <item><description>校内私有 / 公开库：<b>单级</b>，本校院校管理员（<c>ResourceLibraries.Audit</c>）。</description></item>
/// <item><description>院校共建库：<b>两级</b>，主建校院校初审 → 联盟终审（<c>ResourceLibraries.LeagueAudit</c>）。</description></item>
/// <item><description>共建任务交付验收：单级（院校）。</description></item>
/// <item><description>企业认证 / 合作需求：两级（学校初审 → 联盟终审，均为 <c>Cooperation.Review</c>）。</description></item>
/// <item><description>招聘岗位：单级（<c>Employment.ReviewJob</c>）。</description></item>
/// </list>
/// </summary>
public class ReviewPolicy
{
    public ReviewPolicy(ReviewTargetType targetType, IReadOnlyList<ReviewStage> stages)
    {
        TargetType = targetType;
        Stages = stages;
    }

    public ReviewTargetType TargetType { get; }

    public IReadOnlyList<ReviewStage> Stages { get; }

    /// <summary>是否需要联盟终审。</summary>
    public bool RequiresLeague => Stages.Count > 1;

    /// <summary>首个阶段。</summary>
    public ReviewStage FirstStage => Stages[0];

    /// <summary>指定阶段之后是否还有下一阶段。</summary>
    public bool HasNextAfter(ReviewStage stage)
    {
        var index = IndexOf(stage);
        return index >= 0 && index < Stages.Count - 1;
    }

    /// <summary>指定阶段的下一阶段（无下一阶段时返回原阶段，仅为兜底）。</summary>
    public ReviewStage NextAfter(ReviewStage stage)
    {
        var index = IndexOf(stage);
        return index >= 0 && index < Stages.Count - 1 ? Stages[index + 1] : stage;
    }

    private int IndexOf(ReviewStage stage)
    {
        for (var i = 0; i < Stages.Count; i++)
        {
            if (Stages[i] == stage)
            {
                return i;
            }
        }

        return -1;
    }
}

public interface IReviewPolicyResolver
{
    /// <summary>解析（目标类型 + 动作 + 库）对应的审核阶段序列。</summary>
    Task<ReviewPolicy> ResolveAsync(
        ReviewTargetType targetType,
        ReviewActionType actionType,
        Guid? libraryId,
        Guid? contentTenantId = null);

    /// <summary>某阶段所需的权限名。</summary>
    string GetStagePermission(ReviewTargetType targetType, ReviewStage stage);
}

public class ReviewPolicyResolver : IReviewPolicyResolver, ITransientDependency
{
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly IDataFilter _dataFilter;

    public ReviewPolicyResolver(
        IRepository<ResourceLibrary, Guid> libraryRepository,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _dataFilter = dataFilter;
    }

    public async Task<ReviewPolicy> ResolveAsync(
        ReviewTargetType targetType,
        ReviewActionType actionType,
        Guid? libraryId,
        Guid? contentTenantId = null)
    {
        switch (targetType)
        {
            case ReviewTargetType.Library:
            case ReviewTargetType.LibraryPublication:
            case ReviewTargetType.BuildTaskDelivery:
            {
                // 库维度决定是否跨校：共建库才需要联盟终审。
                // 任务交付验收始终单级（验收 != 发布，跨校验收由主责校做）。
                if (targetType == ReviewTargetType.BuildTaskDelivery)
                {
                    return new ReviewPolicy(targetType, new[] { ReviewStage.School });
                }

                var requiresLeague = await RequiresLeagueAsync(libraryId);
                return new ReviewPolicy(
                    targetType,
                    requiresLeague
                        ? new[] { ReviewStage.School, ReviewStage.League }
                        : new[] { ReviewStage.School });
            }

            case ReviewTargetType.EnterpriseProfile:
            case ReviewTargetType.CooperationDemand:
                // 企业主体与合作需求：入驻学校初审 → 联盟终审（跨校可见性由联盟把关）。
                return new ReviewPolicy(
                    targetType,
                    new[] { ReviewStage.School, ReviewStage.League });

            case ReviewTargetType.EmploymentJob:
                // 岗位发布：学校/平台单级审核（沿用既有 ReviewJob 语义）。
                return new ReviewPolicy(targetType, new[] { ReviewStage.School });

            default:
                return new ReviewPolicy(targetType, new[] { ReviewStage.School });
        }
    }

    public string GetStagePermission(ReviewTargetType targetType, ReviewStage stage)
    {
        switch (targetType)
        {
            case ReviewTargetType.Library:
            case ReviewTargetType.LibraryPublication:
            case ReviewTargetType.BuildTaskDelivery:
                return stage == ReviewStage.League
                    ? KnowledgeHubPermissions.ResourceLibraries.LeagueAudit
                    : KnowledgeHubPermissions.ResourceLibraries.Audit;

            case ReviewTargetType.EnterpriseProfile:
            case ReviewTargetType.CooperationDemand:
                return KnowledgeHubPermissions.Cooperation.Review;

            case ReviewTargetType.EmploymentJob:
                return KnowledgeHubPermissions.Employment.ReviewJob;

            default:
                return KnowledgeHubPermissions.ResourceLibraries.Audit;
        }
    }

    /// <summary>
    /// 库是否为「院校共建」形态 —— 只有共建库才需要联盟终审。
    /// 库归属他校（跨租户参与共建）时必须关闭租户过滤器才能查到。
    /// </summary>
    private async Task<bool> RequiresLeagueAsync(Guid? libraryId)
    {
        if (!libraryId.HasValue || libraryId.Value == Guid.Empty)
        {
            return false;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var library = await _libraryRepository.FindAsync(libraryId.Value);
            return library != null && library.Visibility == ResourceLibraryVisibility.CoBuilt;
        }
    }
}
