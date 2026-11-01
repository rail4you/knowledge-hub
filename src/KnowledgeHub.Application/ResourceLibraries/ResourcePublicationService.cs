using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Features;
using KnowledgeHub.Review;
using KnowledgeHub.Review.Dtos;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 库内内容「准入」服务：内容进库必须留下一条 <see cref="ResourcePublication"/> 并走审核。
///
/// <para>
/// 合并审核（决策 C2）：内容还没审核通过时，同一条审核单既审内容、也审准入，
/// 不会出现「先等一次内容审核、再等一次准入审核」。
/// </para>
/// </summary>
public interface IResourcePublicationService
{
    /// <summary>审核开关是否开启（关闭时保持旧的直接入库行为）。</summary>
    Task<bool> IsReviewEnabledAsync();

    /// <summary>
    /// 发起准入：建发布记录（草稿→审核中）并提交审核单。
    /// 内容仍是草稿时由同一条审核单在通过后一并把内容置为已发布。
    /// </summary>
    Task<ResourcePublication> BeginAdmissionAsync(
        Guid libraryId,
        LibraryContentType contentType,
        Guid contentId,
        Guid? contentVersionId,
        string? policyJson = null,
        PublicationSubmitterType submitterType = PublicationSubmitterType.School);

    /// <summary>取某内容的发布记录（没有则 null）。</summary>
    Task<ResourcePublication?> FindAsync(Guid libraryId, LibraryContentType contentType, Guid contentId);

    /// <summary>取某库某类型下「已发布」的内容 Id 集合（库内列表用它过滤）。</summary>
    Task<HashSet<Guid>> GetPublishedContentIdsAsync(Guid libraryId, LibraryContentType contentType);

    /// <summary>取某库某类型下「有过准入记录」的内容 Id 集合（含待审/驳回/下架）。</summary>
    Task<HashSet<Guid>> GetPublicationContentIdsAsync(Guid libraryId, LibraryContentType contentType);

    /// <summary>
    /// 取某库某类型下「有过准入记录」的内容 Id → 提交人（<c>CreatorId</c>）的映射。
    /// <para>
    /// 「未发布内容只有提交人可见」必须按<b>准入提交人</b>判定，
    /// 而不是按内容本身的创建人——教师共享他人创建的资源入库后，
    /// 自己在「库内容」里看不到自己提交的待审项（V2 前端验收 P2-8）。
    /// </para>
    /// </summary>
    Task<Dictionary<Guid, Guid>> GetPublicationSubmitterMapAsync(Guid libraryId, LibraryContentType contentType);

    /// <summary>
    /// 取某库某类型下「已发布 + 审核中」的内容 Id 集合。
    /// 用于资源库卡片 / 列表的「内容数」统计，避免与「库内容」列表口径不一致
    /// （V2 前端验收 P2-9：库里明明有内容，卡片却显示 0 份资源）。
    /// </summary>
    Task<HashSet<Guid>> GetCountedContentIdsAsync(Guid libraryId, LibraryContentType contentType);

    /// <summary>内容下架 / 移出库：发布记录置 <c>Withdrawn</c>（不删除，保留可追溯）。</summary>
    Task WithdrawAsync(Guid libraryId, LibraryContentType contentType, Guid contentId, string? reason = null);

    /// <summary>当前用户是否可以看到某条未发布内容（提交人 / 审核人可见自己的待审项）。</summary>
    bool CanSeeUnpublished(ResourcePublication publication);
}

public class ResourcePublicationService : IResourcePublicationService, ITransientDependency
{
    private readonly IRepository<ResourcePublication, Guid> _publicationRepository;
    private readonly IReviewWorkflow _reviewWorkflow;
    private readonly IReviewPolicyResolver _policyResolver;
    private readonly IFeatureChecker _featureChecker;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<Resources.Resource, Guid> _resourceRepository;
    private readonly IRepository<CourseSharedRef, Guid> _sharedRefRepository;
    private readonly IRepository<Courses.Course, Guid> _courseRepository;
    private readonly IRepository<Practicums.PracticumProject, Guid> _practicumRepository;

    public ResourcePublicationService(
        IRepository<ResourcePublication, Guid> publicationRepository,
        IReviewWorkflow reviewWorkflow,
        IReviewPolicyResolver policyResolver,
        IFeatureChecker featureChecker,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter,
        ICurrentUser currentUser,
        IRepository<Resources.Resource, Guid> resourceRepository,
        IRepository<CourseSharedRef, Guid> sharedRefRepository,
        IRepository<Courses.Course, Guid> courseRepository,
        IRepository<Practicums.PracticumProject, Guid> practicumRepository)
    {
        _publicationRepository = publicationRepository;
        _reviewWorkflow = reviewWorkflow;
        _policyResolver = policyResolver;
        _featureChecker = featureChecker;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
        _currentUser = currentUser;
        _resourceRepository = resourceRepository;
        _sharedRefRepository = sharedRefRepository;
        _courseRepository = courseRepository;
        _practicumRepository = practicumRepository;
    }

    public Task<bool> IsReviewEnabledAsync()
    {
        return _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.LibraryReview);
    }

    public async Task<ResourcePublication> BeginAdmissionAsync(
        Guid libraryId,
        LibraryContentType contentType,
        Guid contentId,
        Guid? contentVersionId,
        string? policyJson = null,
        PublicationSubmitterType submitterType = PublicationSubmitterType.School)
    {
        var existing = await FindAsync(libraryId, contentType, contentId);

        if (existing != null && existing.Status == ResourcePublicationStatus.PendingReview)
        {
            throw new UserFriendlyException("该内容正在审核中，请等待结果或先撤回。");
        }

        if (existing != null && existing.Status == ResourcePublicationStatus.Published)
        {
            throw new UserFriendlyException("该内容已在该资源库中发布。");
        }

        var contentTenantId = await ResolveContentTenantIdAsync(contentType, contentId);
        var contentName = await ResolveContentNameAsync(contentType, contentId);

        var publication = existing ?? new ResourcePublication(
            Guid.NewGuid(),
            libraryId,
            contentType,
            contentId,
            contentTenantId,
            submitterType);

        publication.ContentVersionId = contentVersionId;
        publication.PolicyJson = policyJson;

        if (existing == null)
        {
            await _publicationRepository.InsertAsync(publication, autoSave: true);
        }
        else
        {
            await _publicationRepository.UpdateAsync(publication, autoSave: true);
        }

        var ticket = await _reviewWorkflow.SubmitAsync(new SubmitReviewInput
        {
            TargetType = ReviewTargetType.LibraryPublication,
            TargetId = publication.Id,
            ActionType = ReviewActionType.Admit,
            LibraryId = libraryId,
            ContentTenantId = contentTenantId,
            TargetName = contentName,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                publicationId = publication.Id,
                libraryId,
                contentType = (byte)contentType,
                contentId,
                contentVersionId,
                contentTenantId,
                policyJson,
                submitterType = (byte)submitterType
            })
        });

        publication.MarkPendingReview(ticket.Id);
        await _publicationRepository.UpdateAsync(publication, autoSave: true);

        return publication;
    }

    public async Task<ResourcePublication?> FindAsync(
        Guid libraryId,
        LibraryContentType contentType,
        Guid contentId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _publicationRepository.GetQueryableAsync();
            return await _asyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.LibraryId == libraryId
                                 && x.ContentType == contentType
                                 && x.ContentId == contentId));
        }
    }

    public async Task<HashSet<Guid>> GetPublishedContentIdsAsync(Guid libraryId, LibraryContentType contentType)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _publicationRepository.GetQueryableAsync();
            var ids = await _asyncExecuter.ToListAsync(
                query.Where(x => x.LibraryId == libraryId
                                 && x.ContentType == contentType
                                 && x.Status == ResourcePublicationStatus.Published)
                     .Select(x => x.ContentId));

            return ids.ToHashSet();
        }
    }

    public async Task<HashSet<Guid>> GetPublicationContentIdsAsync(Guid libraryId, LibraryContentType contentType)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _publicationRepository.GetQueryableAsync();
            var ids = await _asyncExecuter.ToListAsync(
                query.Where(x => x.LibraryId == libraryId && x.ContentType == contentType)
                     .Select(x => x.ContentId));

            return ids.ToHashSet();
        }
    }

    public async Task<HashSet<Guid>> GetCountedContentIdsAsync(
        Guid libraryId,
        LibraryContentType contentType)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _publicationRepository.GetQueryableAsync();
            var ids = await _asyncExecuter.ToListAsync(
                query.Where(x => x.LibraryId == libraryId
                                 && x.ContentType == contentType
                                 && (x.Status == ResourcePublicationStatus.Published
                                     || x.Status == ResourcePublicationStatus.PendingReview))
                     .Select(x => x.ContentId));

            return ids.ToHashSet();
        }
    }

    public async Task<Dictionary<Guid, Guid>> GetPublicationSubmitterMapAsync(
        Guid libraryId,
        LibraryContentType contentType)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _publicationRepository.GetQueryableAsync();
            var rows = await _asyncExecuter.ToListAsync(
                query.Where(x => x.LibraryId == libraryId && x.ContentType == contentType)
                     .Select(x => new { x.ContentId, x.CreatorId }));

            return rows
                .Where(x => x.CreatorId.HasValue && x.CreatorId.Value != Guid.Empty)
                .GroupBy(x => x.ContentId)
                .ToDictionary(g => g.Key, g => g.First().CreatorId!.Value);
        }
    }

    public async Task WithdrawAsync(Guid libraryId, LibraryContentType contentType, Guid contentId, string? reason = null)
    {
        var publication = await FindAsync(libraryId, contentType, contentId);
        if (publication == null)
        {
            return;
        }

        if (publication.Status == ResourcePublicationStatus.Withdrawn)
        {
            return;
        }

        publication.MarkWithdrawn(reason);
        await _publicationRepository.UpdateAsync(publication, autoSave: true);

        // V2.0：来源下架 / 移出库 → 自动撤销对它的跨校引用，
        // 否则学生端仍会看到"引用生效"却打不开文件（403），体验与口径都不一致。
        if (contentType == LibraryContentType.Resource)
        {
            await RevokeActiveRefsAsync(libraryId, contentId, reason);
        }
    }

    /// <summary>撤销指向「某库某内容」的全部生效中引用（来源下架联动）。</summary>
    private async Task RevokeActiveRefsAsync(Guid libraryId, Guid contentId, string? reason)
    {
        try
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var query = await _sharedRefRepository.GetQueryableAsync();
                var refs = await _asyncExecuter.ToListAsync(
                    query.Where(x => x.LibraryId == libraryId
                                     && x.ContentId == contentId
                                     && x.Status == CourseSharedRefStatus.Active));

                foreach (var reference in refs)
                {
                    reference.Revoke(null, string.IsNullOrWhiteSpace(reason)
                        ? "来源资源已下架，引用自动撤销"
                        : reason);
                    await _sharedRefRepository.UpdateAsync(reference, autoSave: true);
                }
            }
        }
        catch
        {
            // 联动失败不阻断下架主流程（教师仍可手动撤销）
        }
    }

    public bool CanSeeUnpublished(ResourcePublication publication)
    {
        var userId = _currentUser.Id;
        return userId.HasValue &&
               (publication.CreatorId == userId.Value || publication.LastModifierId == userId.Value);
    }

    private async Task<Guid?> ResolveContentTenantIdAsync(LibraryContentType contentType, Guid contentId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            switch (contentType)
            {
                case LibraryContentType.Resource:
                    return (await _resourceRepository.FindAsync(contentId))?.TenantId;
                case LibraryContentType.Course:
                    return (await _courseRepository.FindAsync(contentId))?.TenantId;
                case LibraryContentType.Practicum:
                    return (await _practicumRepository.FindAsync(contentId))?.TenantId;
                default:
                    return null;
            }
        }
    }

    private async Task<string?> ResolveContentNameAsync(LibraryContentType contentType, Guid contentId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return contentType switch
            {
                LibraryContentType.Resource => (await _resourceRepository.FindAsync(contentId))?.Name,
                LibraryContentType.Course => (await _courseRepository.FindAsync(contentId))?.Title,
                LibraryContentType.Practicum => (await _practicumRepository.FindAsync(contentId))?.Title,
                _ => null
            };
        }
    }
}
