using System;
using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.Resources.Media;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 实现的资源媒体处理队列（PostgreSQL 持久化）。
/// 入队到 "media" 专用队列，与 conversion/ai 任务隔离。
/// </summary>
public class HangfireResourceMediaJobQueue : IResourceMediaJobQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireResourceMediaJobQueue(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task EnqueueAsync(Guid jobId, Guid? tenantId)
    {
        // 经 Bridge 入队以接入 Hangfire 停机信号（IJobCancellationToken 由服务端注入，
        // 入队时传 Null 占位）。
        _backgroundJobClient.Enqueue<ResourceMediaHangfireBridge>(
            "media",
            job => job.ExecuteAsync(jobId, tenantId, JobCancellationToken.Null));
        return Task.CompletedTask;
    }
}
