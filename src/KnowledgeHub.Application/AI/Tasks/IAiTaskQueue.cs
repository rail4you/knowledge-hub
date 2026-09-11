using System;
using System.Threading.Tasks;

namespace KnowledgeHub.Application.AI.Tasks;

/// <summary>
/// AI 生成任务队列抽象。由 Host 层用 Hangfire（PostgreSQL 持久化）实现，
/// 业务代码只依赖此接口，避免直接依赖 Hangfire。
/// </summary>
public interface IAiTaskQueue
{
    /// <summary>将已落库的 AI 任务入队到后台执行。</summary>
    Task EnqueueAsync(Guid taskId, Guid? tenantId);
}
