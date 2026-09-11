using System;
using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.Application.AI.Tasks;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 实现的 AI 生成任务队列（PostgreSQL 持久化）。
/// 入队到 "ai" 专用队列，与转换任务隔离，避免长任务互相阻塞。
/// </summary>
public class HangfireAiTaskQueue : IAiTaskQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireAiTaskQueue(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task EnqueueAsync(Guid taskId, Guid? tenantId)
    {
        _backgroundJobClient.Enqueue<AiGenerationJob>(
            "ai",
            job => job.ExecuteAsync(taskId, tenantId));
        return Task.CompletedTask;
    }
}
