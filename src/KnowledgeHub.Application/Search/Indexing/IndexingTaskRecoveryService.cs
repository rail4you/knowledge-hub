using System;
using System.Threading.Tasks;
using KnowledgeHub.Domain.Search;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Search.Indexing;

/// <summary>
/// 索引任务恢复：由 Hangfire 定时调用。
/// 索引任务已接入 Hangfire（持久化），正常不会丢；此服务作为兜底：
///  - Running/Parsing/Indexing 超过阈值仍无进展 → 判定进程中断，标记 Failed；
///  - Pending 超过更长阈值（任务可能已被删除/过期）→ 标记 Failed，避免永久停留在“排队中”。
/// </summary>
public class IndexingTaskRecoveryService : ITransientDependency
{
    private const int RunningStaleMinutes = 60;
    private const int PendingStaleMinutes = 360;

    private readonly IRepository<DocumentIndexingJob, Guid> _documentJobRepository;
    private readonly IRepository<VideoIndexingJob, Guid> _videoJobRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<IndexingTaskRecoveryService> _logger;

    public IndexingTaskRecoveryService(
        IRepository<DocumentIndexingJob, Guid> documentJobRepository,
        IRepository<VideoIndexingJob, Guid> videoJobRepository,
        IDataFilter dataFilter,
        ILogger<IndexingTaskRecoveryService> logger)
    {
        _documentJobRepository = documentJobRepository;
        _videoJobRepository = videoJobRepository;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task RecoverAsync()
    {
        var runningThreshold = DateTime.UtcNow.AddMinutes(-RunningStaleMinutes);
        var pendingThreshold = DateTime.UtcNow.AddMinutes(-PendingStaleMinutes);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var docJobs = await _documentJobRepository.GetListAsync(x =>
                ((x.Status == IndexingJobStatus.Parsing || x.Status == IndexingJobStatus.Indexing) &&
                 x.StartedAt != null && x.StartedAt < runningThreshold) ||
                (x.Status == IndexingJobStatus.Pending && x.CreationTime < pendingThreshold));

            foreach (var job in docJobs)
            {
                _logger.LogWarning("[IndexingRecovery] 标记文档索引任务 {JobId}（{Status}）为失败", job.Id, job.Status);
                job.Status = IndexingJobStatus.Failed;
                job.ErrorMessage = "任务中断或超时（服务重启/异常终止），请重试。";
                job.CompletedAt = DateTime.UtcNow;
                await _documentJobRepository.UpdateAsync(job);
            }

            var videoJobs = await _videoJobRepository.GetListAsync(x =>
                ((x.Status == VideoIndexingJobStatus.Parsing ||
                  x.Status == VideoIndexingJobStatus.Analyzing ||
                  x.Status == VideoIndexingJobStatus.Indexing) &&
                 x.StartedAt != null && x.StartedAt < runningThreshold) ||
                (x.Status == VideoIndexingJobStatus.Pending && x.CreationTime < pendingThreshold));

            foreach (var job in videoJobs)
            {
                _logger.LogWarning("[IndexingRecovery] 标记视频索引任务 {JobId}（{Status}）为失败", job.Id, job.Status);
                job.Status = VideoIndexingJobStatus.Failed;
                job.ErrorMessage = "任务中断或超时（服务重启/异常终止），请重试。";
                job.CompletedAt = DateTime.UtcNow;
                await _videoJobRepository.UpdateAsync(job);
            }
        }
    }
}
