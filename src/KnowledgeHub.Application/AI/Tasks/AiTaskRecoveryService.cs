using System;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Application.AI.Tasks;

/// <summary>
/// AI 任务恢复：由 Hangfire 定时（每 5 分钟）调用。
/// 将长时间停留在 Running 的任务判定为进程中断/超时并标记失败，
/// 避免任务永久卡在"生成中"（Hangfire 默认不会自动重排崩溃前的 Processing 任务）。
/// 说明：Pending 任务不做自动重排，避免与仍在 Hangfire 队列中的同一任务重复执行
///（尤其习题生成会重复入库）；卡住的 Pending 任务可在任务监控页取消后重试。
/// </summary>
public class AiTaskRecoveryService : ITransientDependency
{
    private const int RunningStaleMinutes = 60;

    private readonly IRepository<AiGenerationTask, Guid> _taskRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<AiTaskRecoveryService> _logger;

    public AiTaskRecoveryService(
        IRepository<AiGenerationTask, Guid> taskRepository,
        IDataFilter dataFilter,
        ILogger<AiTaskRecoveryService> logger)
    {
        _taskRepository = taskRepository;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task RecoverAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-RunningStaleMinutes);

        // 后台作业无当前租户，需关闭多租户过滤才能覆盖所有租户的任务。
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var staleRunning = await _taskRepository.GetListAsync(x =>
                x.Status == AiTaskStatus.Running && x.StartedAt != null && x.StartedAt < threshold);

            foreach (var task in staleRunning)
            {
                _logger.LogWarning("[AiTaskRecovery] 标记中断/超时的 Running 任务 {TaskId} 为失败", task.Id);
                task.Status = AiTaskStatus.Failed;
                task.ProgressMessage = "已中断";
                task.ErrorMessage = "任务中断或超时（服务重启/异常终止），请重试。";
                task.CompletedAt = DateTime.UtcNow;
                task.IsRead = false;
                await _taskRepository.UpdateAsync(task);
            }
        }
    }
}

