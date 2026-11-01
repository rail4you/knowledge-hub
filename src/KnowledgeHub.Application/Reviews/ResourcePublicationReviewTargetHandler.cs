using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.Review;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Reviews;

/// <summary>
/// 库内内容准入的审核落地处理器（<see cref="ReviewTargetType.LibraryPublication"/>）。
///
/// <para>终审通过后一次性完成三件事：</para>
/// <list type="number">
/// <item><description>发布记录置 <c>Published</c>（库内开始可见）；</description></item>
/// <item><description>把内容挂到库里（<c>LibraryId</c> 主字段为空时补写 + 写关联表）；</description></item>
/// <item><description>若内容此前还是草稿（合并审核），一并置为 <c>LeagueApproved</c>。</description></item>
/// </list>
///
/// <para>驳回时只把发布记录置 <c>Rejected</c>，内容保持原状（草稿仍是草稿，不污染内容审核状态）。</para>
/// </summary>
public class ResourcePublicationReviewTargetHandler : IReviewTargetHandler, ITransientDependency
{
    private readonly IRepository<ResourcePublication, Guid> _publicationRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<KnowledgeHub.Courses.Course, Guid> _courseRepository;
    private readonly IRepository<KnowledgeHub.Practicums.PracticumProject, Guid> _practicumRepository;
    private readonly IRepository<ResourceLibraryResource, Guid> _linkRepository;
    private readonly IDataFilter _dataFilter;
    private readonly KnowledgeHub.Search.Indexing.IIndexingJobQueue _indexingJobQueue;
    private readonly KnowledgeHub.Application.Contracts.Search.IMeiliSearchService _meiliSearchService;
    private readonly ILogger<ResourcePublicationReviewTargetHandler> _logger;

    public ResourcePublicationReviewTargetHandler(
        IRepository<ResourcePublication, Guid> publicationRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<KnowledgeHub.Courses.Course, Guid> courseRepository,
        IRepository<KnowledgeHub.Practicums.PracticumProject, Guid> practicumRepository,
        IRepository<ResourceLibraryResource, Guid> linkRepository,
        IDataFilter dataFilter,
        KnowledgeHub.Search.Indexing.IIndexingJobQueue indexingJobQueue,
        KnowledgeHub.Application.Contracts.Search.IMeiliSearchService meiliSearchService,
        ILogger<ResourcePublicationReviewTargetHandler> logger)
    {
        _publicationRepository = publicationRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _practicumRepository = practicumRepository;
        _linkRepository = linkRepository;
        _dataFilter = dataFilter;
        _indexingJobQueue = indexingJobQueue;
        _meiliSearchService = meiliSearchService;
        _logger = logger;
    }

    public ReviewTargetType TargetType => ReviewTargetType.LibraryPublication;

    public async Task OnApprovedAsync(ReviewTicket ticket)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var publication = await _publicationRepository.FindAsync(ticket.TargetId);
            if (publication == null)
            {
                _logger.LogWarning("[PublicationReview] 发布记录 {Id} 不存在，跳过落地。", ticket.TargetId);
                return;
            }

            publication.MarkPublished(publication.PolicyJson, publication.ContentVersionId);
            await _publicationRepository.UpdateAsync(publication, autoSave: true);

            await AttachContentToLibraryAsync(publication, ticket);

            // V2.0 索引联动：发布后重灌索引，使 libraryId / visibility / status 与实际一致
            // （否则检索仍按旧文档判断，跨校资源搜不到或已撤下的仍可见）。
            if (publication.ContentType == LibraryContentType.Resource)
            {
                try
                {
                    var res = await _resourceRepository.FindAsync(publication.ContentId);
                    if (res != null)
                    {
                        // 先做轻量元数据同步（不依赖文件解析，必定生效）；
                        // 再入队整体重灌（文件可解析时补全分页内容）。
                        await _meiliSearchService.SyncResourceMetadataAsync(res.Id);

                        await _indexingJobQueue.EnqueueDocumentAsync(
                            new KnowledgeHub.Application.Contracts.Search.DocumentIndexingJobArgs
                            {
                                JobId = Guid.NewGuid(),
                                ResourceId = res.Id,
                                FilePath = res.FilePath,
                                TenantId = res.TenantId
                            });
                    }
                }
                catch (Exception ex)
                {
                    // 索引入队失败不影响审核落地（后台有重灌工具兜底）
                    _logger.LogWarning(ex, "[PublicationReview] 索引入队失败 resource={ResourceId}", publication.ContentId);
                }
            }
        }
    }

    public async Task OnRejectedAsync(ReviewTicket ticket)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var publication = await _publicationRepository.FindAsync(ticket.TargetId);
            if (publication == null)
            {
                return;
            }

            publication.MarkRejected(ticket.Reason);
            await _publicationRepository.UpdateAsync(publication, autoSave: true);
        }
    }

    public async Task<string?> ResolveTargetNameAsync(ReviewTicket ticket)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var publication = await _publicationRepository.FindAsync(ticket.TargetId);
            if (publication == null)
            {
                return null;
            }

            return publication.ContentType switch
            {
                LibraryContentType.Resource => (await _resourceRepository.FindAsync(publication.ContentId))?.Name,
                LibraryContentType.Course => (await _courseRepository.FindAsync(publication.ContentId))?.Title,
                LibraryContentType.Practicum => (await _practicumRepository.FindAsync(publication.ContentId))?.Title,
                _ => null
            };
        }
    }

    /// <summary>把内容挂进库：主库字段为空才补写（不抢占别的主库），并写关联表。</summary>
    private async Task AttachContentToLibraryAsync(ResourcePublication publication, ReviewTicket ticket)
    {
        switch (publication.ContentType)
        {
            case LibraryContentType.Resource:
            {
                var resource = await _resourceRepository.FindAsync(publication.ContentId);
                if (resource == null)
                {
                    _logger.LogWarning("[PublicationReview] 资源 {Id} 不存在。", publication.ContentId);
                    return;
                }

                if (!resource.LibraryId.HasValue)
                {
                    resource.LibraryId = publication.LibraryId;
                }

                // 合并审核：内容还是草稿 → 准入通过即视为内容审核通过（两级都已完成）
                if (resource.Status is ResourceStatus.Draft or ResourceStatus.Rejected or ResourceStatus.PendingReview)
                {
                    resource.Status = ResourceStatus.LeagueApproved;
                }

                await _resourceRepository.UpdateAsync(resource, autoSave: true);
                break;
            }

            case LibraryContentType.Course:
            {
                var course = await _courseRepository.FindAsync(publication.ContentId);
                if (course == null)
                {
                    return;
                }

                if (!course.LibraryId.HasValue)
                {
                    course.LibraryId = publication.LibraryId;
                }

                await _courseRepository.UpdateAsync(course, autoSave: true);
                break;
            }

            case LibraryContentType.Practicum:
            {
                var practicum = await _practicumRepository.FindAsync(publication.ContentId);
                if (practicum == null)
                {
                    return;
                }

                if (!practicum.LibraryId.HasValue)
                {
                    practicum.LibraryId = publication.LibraryId;
                }

                await _practicumRepository.UpdateAsync(practicum, autoSave: true);
                break;
            }
        }

        // 多对多关联表：一个内容可同时挂多个库（幂等由唯一索引兜底）
        var linkQuery = await _linkRepository.GetQueryableAsync();
        var existingLink = await AsyncFirstOrDefaultAsync(linkQuery, publication);
        if (existingLink == null)
        {
            await _linkRepository.InsertAsync(
                new ResourceLibraryResource(
                    Guid.NewGuid(),
                    publication.LibraryId,
                    publication.ContentId,
                    publication.ContentTenantId),
                autoSave: true);
        }
    }

    private static async Task<ResourceLibraryResource?> AsyncFirstOrDefaultAsync(
        IQueryable<ResourceLibraryResource> query,
        ResourcePublication publication)
    {
        // 关联表只对「资源」建行（课程/实训有独立关联表）
        if (publication.ContentType != LibraryContentType.Resource)
        {
            return null;
        }

        return await Task.FromResult(
            System.Linq.Queryable.FirstOrDefault(
                query.Where(x => x.LibraryId == publication.LibraryId && x.ResourceId == publication.ContentId)));
    }
}
