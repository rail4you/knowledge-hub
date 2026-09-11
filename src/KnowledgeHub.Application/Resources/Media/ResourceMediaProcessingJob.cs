using System;
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

    public async Task ExecuteAsync(Guid jobId, Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var job = await GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("ResourceMediaProcessingJob: job {JobId} not found", jobId);
                return;
            }

            if (job.Status is ResourceMediaJobStatus.Completed or ResourceMediaJobStatus.Cancelled)
            {
                return;
            }

            await UpdateJobAsync(jobId, j =>
            {
                j.Status = ResourceMediaJobStatus.Running;
                j.StartedAt ??= DateTime.UtcNow;
                j.Progress = 1;
                j.ProgressMessage = "开始处理…";
                j.ErrorMessage = null;
            });

            try
            {
                using var uow = _unitOfWorkManager.Begin(requiresNew: true);
                var outcome = await _processor.ProcessAsync(
                    job.ResourceId,
                    job.ResourceVersionId,
                    (progress, message) => UpdateProgressAsync(jobId, progress, message),
                    CancellationToken.None);
                await uow.CompleteAsync();

                await UpdateJobAsync(jobId, j =>
                {
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
