using System;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources.Media;
using KnowledgeHub.Search.Indexing;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源后台任务的统一定义与重试（媒体 / 文档索引 / 视频索引）。
/// 三个任务表相互独立，可分别重试；供资源进度与资源任务页面共用。
/// </summary>
public class ResourceTaskRetryService : ITransientDependency
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<DocumentIndexingJob, Guid> _documentJobRepository;
    private readonly IRepository<VideoIndexingJob, Guid> _videoJobRepository;
    private readonly ResourceMediaJobManager _mediaJobManager;
    private readonly IIndexingJobQueue _indexingJobQueue;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public ResourceTaskRetryService(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<DocumentIndexingJob, Guid> documentJobRepository,
        IRepository<VideoIndexingJob, Guid> videoJobRepository,
        ResourceMediaJobManager mediaJobManager,
        IIndexingJobQueue indexingJobQueue,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _resourceRepository = resourceRepository;
        _documentJobRepository = documentJobRepository;
        _videoJobRepository = videoJobRepository;
        _mediaJobManager = mediaJobManager;
        _indexingJobQueue = indexingJobQueue;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    public async Task RetryAsync(string kind, Guid taskId)
    {
        // host（无当前租户）需跨租户重试；租户管理员保持过滤器，仅能重试本租户任务。
        using var _ = _currentTenant.Id == null ? _dataFilter.Disable<IMultiTenant>() : null;

        switch (kind)
        {
            case "media":
                await _mediaJobManager.RetryAsync(taskId);
                break;
            case "document-index":
                await RetryDocumentIndexAsync(taskId);
                break;
            case "video-index":
                await RetryVideoIndexAsync(taskId);
                break;
            default:
                throw new UserFriendlyException("不支持的任务类型");
        }
    }

    private async Task RetryDocumentIndexAsync(Guid jobId)
    {
        var job = await _documentJobRepository.FindAsync(jobId)
            ?? throw new EntityNotFoundException(typeof(DocumentIndexingJob), jobId);

        var resource = await _resourceRepository.GetAsync(job.ResourceId);

        job.Status = IndexingJobStatus.Pending;
        job.ErrorMessage = null;
        job.RetryCount = 0;
        job.NextRetryAt = null;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.Progress = 0;
        job.ProcessedPages = null;
        await _documentJobRepository.UpdateAsync(job);

        await _indexingJobQueue.EnqueueDocumentAsync(new DocumentIndexingJobArgs
        {
            JobId = job.Id,
            ResourceId = job.ResourceId,
            FilePath = resource.FilePath,
            TenantId = job.TenantId ?? _currentTenant.Id,
            ResourceVersionId = job.ResourceVersionId
        });
    }

    private async Task RetryVideoIndexAsync(Guid jobId)
    {
        var job = await _videoJobRepository.FindAsync(jobId)
            ?? throw new EntityNotFoundException(typeof(VideoIndexingJob), jobId);

        var resource = await _resourceRepository.GetAsync(job.ResourceId);

        job.Status = VideoIndexingJobStatus.Pending;
        job.ErrorMessage = null;
        job.RetryCount = 0;
        job.NextRetryAt = null;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.Progress = 0;
        job.ProcessedEvents = null;
        await _videoJobRepository.UpdateAsync(job);

        await _indexingJobQueue.EnqueueVideoAsync(new VideoIndexingJobArgs
        {
            JobId = job.Id,
            ResourceId = job.ResourceId,
            FilePath = resource.FilePath,
            TenantId = job.TenantId ?? _currentTenant.Id
        });
    }
}
