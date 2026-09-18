using System;
using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.Resources.Media;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 调用桥：把 Hangfire 的停机信号（<see cref="IJobCancellationToken"/>）
/// 转为 <see cref="System.Threading.CancellationToken"/> 交给应用层任务。
/// Application 层不引用 Hangfire，故关机信号只能在 Host 层接入。
/// </summary>
public class ResourceMediaHangfireBridge
{
    private readonly ResourceMediaProcessingJob _inner;

    public ResourceMediaHangfireBridge(ResourceMediaProcessingJob inner)
    {
        _inner = inner;
    }

    // 注意：不加 AutomaticRetry(Attempts=0)——应用层已自行捕获业务异常并落库，
    // 漏到 Hangfire 的只剩基础设施异常（DB/Redis 瞬断），保留默认重试更稳。
    [Queue("media")]
    public Task ExecuteAsync(Guid jobId, Guid? tenantId, IJobCancellationToken cancellationToken)
    {
        // 关机/取消时 ffmpeg / Gotenberg 调用会被中断，
        // 应用层把任务打回 Pending，其他实例可尽快接管。
        return _inner.ExecuteAsync(jobId, tenantId, cancellationToken.ShutdownToken);
    }
}
