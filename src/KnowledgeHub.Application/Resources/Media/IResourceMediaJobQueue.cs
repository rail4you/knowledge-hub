using System;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源媒体处理任务队列抽象。由 Host 层用 Hangfire（PostgreSQL 持久化）实现，
/// 入队到独立 "media" 队列，与转换/AI/索引任务隔离。
/// </summary>
public interface IResourceMediaJobQueue
{
    Task EnqueueAsync(Guid jobId, Guid? tenantId);
}
