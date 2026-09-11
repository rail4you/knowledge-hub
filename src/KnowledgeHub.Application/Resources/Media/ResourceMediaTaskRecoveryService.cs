using System;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 媒体处理任务恢复：由 Hangfire 定时（每 5 分钟）调用。
/// 将长时间停留在 Running 的任务判定为进程中断/超时并标记失败，
/// 避免任务永久卡在"处理中"（Hangfire 不会自动重排崩溃前的任务）。
/// Pending 任务不自动重排，避免与队列中的同一任务重复执行；可在管理页重试。
/// </summary>
public class ResourceMediaTaskRecoveryService : ITransientDependency
{
    private const int RunningStaleMinutes = 60;

    private readonly IRepository<ResourceMediaJob, Guid> _jobRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<ResourceMediaTaskRecoveryService> _logger;

    public ResourceMediaTaskRecoveryService(
        IRepository<ResourceMediaJob, Guid> jobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IDataFilter dataFilter,
        ILogger<ResourceMediaTaskRecoveryService> logger)
    {
        _jobRepository = jobRepository;
        _resourceRepository = resourceRepository;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task RecoverAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-RunningStaleMinutes);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var stale = await _jobRepository.GetListAsync(x =>
                x.Status == ResourceMediaJobStatus.Running &&
                x.StartedAt != null &&
                x.StartedAt < threshold);

            foreach (var job in stale)
            {
                _logger.LogWarning("[MediaRecovery] 标记中断/超时的 Running 任务 {JobId} 为失败", job.Id);
                job.Status = ResourceMediaJobStatus.Failed;
                job.ProgressMessage = "已中断";
                job.ErrorMessage = "任务中断或超时（服务重启/异常终止），请重试。";
                job.CompletedAt = DateTime.UtcNow;
                job.IsRead = false;
                await _jobRepository.UpdateAsync(job);

                var resource = await _resourceRepository.FindAsync(job.ResourceId);
                if (resource != null)
                {
                    resource.MediaStatus = ResourceMediaStatus.Failed;
                    await _resourceRepository.UpdateAsync(resource);
                }
            }
        }
    }
}
