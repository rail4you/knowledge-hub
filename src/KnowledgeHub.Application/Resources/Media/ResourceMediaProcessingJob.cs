using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// Hangfire 执行的资源媒体处理任务：驱动 <see cref="ResourceMediaProcessor"/>，
/// 边执行边刷新 ResourceMediaJob 进度/状态，并更新 Resource.MediaStatus。
/// </summary>
public class ResourceMediaProcessingJob : ITransientDependency
{
    private readonly IRepository<ResourceMediaJob, Guid> _jobRepository;
    private readonly ResourceMediaProcessor _processor;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ResourceMediaProcessingJob> _logger;

    public ResourceMediaProcessingJob(
        IRepository<ResourceMediaJob, Guid> jobRepository,
        ResourceMediaProcessor processor,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        ILogger<ResourceMediaProcessingJob> logger)
    {
        _jobRepository = jobRepository;
        _processor = processor;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    // 同一 jobId 只允许一个实例执行，防止重复入队导致进度/状态互相覆盖。
    // 两层保护：
    // 1) 进程内 RunningJobs：同进程重复投递快速跳过；
    // 2) DB 认领（TryClaimAsync）：跨实例生效。Running 且 StartedAt 在阈值内的任务
    //    直接跳过；Pending→Running 的认领依赖 ConcurrencyStamp 乐观并发，
    //    抢输的实例抛并发异常后跳过。Application 层不引用 Hangfire，
    //    故不用 [DisableConcurrentExecution]。
    private static readonly ConcurrentDictionary<Guid, byte> RunningJobs = new();

    /// <summary>Running 认领有效期：超过即视为中断实例，可被其他实例接管（与恢复服务阈值对齐）。</summary>
    private static readonly TimeSpan ClaimStaleAfter = TimeSpan.FromMinutes(60);

    public async Task ExecuteAsync(Guid jobId, Guid? tenantId, CancellationToken ct = default)
    {
        if (!RunningJobs.TryAdd(jobId, 0))
        {
            _logger.LogWarning(
                "ResourceMediaProcessingJob: job {JobId} is already running; skip duplicate execution.", jobId);
            return;
        }

        try
        {
            await ExecuteInternalAsync(jobId, tenantId, ct);
        }
        finally
        {
            RunningJobs.TryRemove(jobId, out _);
        }
    }

    private async Task ExecuteInternalAsync(Guid jobId, Guid? tenantId, CancellationToken ct)
    {
        using (_currentTenant.Change(tenantId))
        {
            var claimed = await TryClaimAsync(jobId);
            if (!claimed)
            {
                return;
            }

            // 认领后重读资源定位（Tenant 上下文内），避免使用认领前的快照
            var job = await GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("ResourceMediaProcessingJob: job {JobId} not found", jobId);
                return;
            }

            try
            {
                using var uow = _unitOfWorkManager.Begin(requiresNew: true);
                var outcome = await _processor.ProcessAsync(
                    job.ResourceId,
                    job.ResourceVersionId,
                    (progress, message) => UpdateProgressAsync(jobId, progress, message),
                    ct);
                await uow.CompleteAsync();

                await UpdateJobAsync(jobId, j =>
                {
                    // 处理器可能按 Resource.FilePath 解析出比入队时更新的版本，
                    // 回写以保证任务页展示的产物与该版本一致。
                    if (outcome.ResourceVersionId.HasValue &&
                        outcome.ResourceVersionId.Value != j.ResourceVersionId)
                    {
                        j.ResourceVersionId = outcome.ResourceVersionId;
                    }

                    j.Status = outcome.Status switch
                    {
                        MediaProcessStatus.Completed => ResourceMediaJobStatus.Completed,
                        MediaProcessStatus.PartialFailed => ResourceMediaJobStatus.PartialFailed,
                        _ => ResourceMediaJobStatus.Failed
                    };
                    j.Progress = 100;
                    j.ProgressMessage = outcome.Status == MediaProcessStatus.Completed
                        ? "处理完成"
                        : outcome.Status == MediaProcessStatus.PartialFailed ? "部分生成物失败" : "处理失败";
                    j.ErrorMessage = outcome.Error;
                    j.CompletedAt = DateTime.UtcNow;
                    // 失败/部分失败时置未读，驱动顶栏通知
                    j.IsRead = outcome.Status == MediaProcessStatus.Completed;
                });
            }
            catch (OperationCanceledException)
            {
                // Hangfire 关机/取消：不标失败，回到 Pending 让其他实例尽快接管
                // （若进程直接退出写库失败，恢复服务会在阈值后兜底）。
                _logger.LogWarning("ResourceMediaProcessingJob: job {JobId} cancelled, requeue to pending", jobId);
                await UpdateJobAsync(jobId, j =>
                {
                    if (j.Status == ResourceMediaJobStatus.Running)
                    {
                        j.Status = ResourceMediaJobStatus.Pending;
                        j.ProgressMessage = "已中断，等待重新执行…";
                        j.StartedAt = null;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResourceMediaProcessingJob: job {JobId} failed", jobId);
                await UpdateJobAsync(jobId, j =>
                {
                    j.Status = ResourceMediaJobStatus.Failed;
                    j.ErrorMessage = Truncate(ex.Message, 2000);
                    j.ProgressMessage = "处理失败";
                    j.CompletedAt = DateTime.UtcNow;
                    j.IsRead = false;
                });
            }
        }
    }

    /// <summary>
    /// DB 认领：Pending（或超时的 Running）→ Running。跨实例生效的互斥点：
    /// 另一实例已认领（Running 且未过期）则返回 false；并发抢认领时输家
    /// 因 ConcurrencyStamp 冲突抛异常，捕获后同样返回 false。
    /// </summary>
    /// <returns>true=认领成功可执行；false=无需执行（不存在/已终态/被他实例持有）。</returns>
    private async Task<bool> TryClaimAsync(Guid jobId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var job = await _jobRepository.FindAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("ResourceMediaProcessingJob: job {JobId} not found", jobId);
            await uow.CompleteAsync();
            return false;
        }

        if (job.Status is ResourceMediaJobStatus.Completed or ResourceMediaJobStatus.Cancelled)
        {
            await uow.CompleteAsync();
            return false;
        }

        if (job.Status == ResourceMediaJobStatus.Running
            && job.StartedAt.HasValue
            && DateTime.UtcNow - job.StartedAt.Value < ClaimStaleAfter)
        {
            _logger.LogWarning(
                "ResourceMediaProcessingJob: job {JobId} is already running on another instance; skip.", jobId);
            await uow.CompleteAsync();
            return false;
        }

        job.Status = ResourceMediaJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        job.Progress = 1;
        job.ProgressMessage = "开始处理…";
        job.ErrorMessage = null;

        try
        {
            await _jobRepository.UpdateAsync(job);
            await uow.CompleteAsync();
            return true;
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            _logger.LogWarning(ex,
                "ResourceMediaProcessingJob: job {JobId} claim lost optimistic-concurrency race; skip.", jobId);
            return false;
        }
    }

    private static bool IsConcurrencyConflict(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return true;
            }

            var name = e.GetType().Name;
            if (name.Contains("Concurrency", StringComparison.Ordinal)
                || name.Contains("AbpDbConcurrency", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<ResourceMediaJob?> GetJobAsync(Guid jobId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var job = await _jobRepository.FindAsync(jobId);
        await uow.CompleteAsync();
        return job;
    }

    private async Task UpdateProgressAsync(Guid jobId, int progress, string? message)
    {
        await UpdateJobAsync(jobId, j =>
        {
            j.Progress = Math.Clamp(progress, 0, 99);
            if (!string.IsNullOrWhiteSpace(message))
            {
                j.ProgressMessage = Truncate(message, 500);
            }
        });
    }

    private async Task UpdateJobAsync(Guid jobId, Action<ResourceMediaJob> mutate)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var job = await _jobRepository.FindAsync(jobId);
        if (job == null)
        {
            await uow.CompleteAsync();
            return;
        }
        mutate(job);
        await _jobRepository.UpdateAsync(job);
        await uow.CompleteAsync();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}
