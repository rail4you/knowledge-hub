using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;

namespace KnowledgeHub.Search.Indexing;

/// <summary>
/// 文档/视频索引任务队列抽象。
/// 由 Host 层用 Hangfire（PostgreSQL 持久化）实现，入队到独立 "indexing" 队列，
/// 避免此前使用 ABP 内存 BackgroundJobManager 导致重启丢任务、job 表永久 Pending。
/// </summary>
public interface IIndexingJobQueue
{
    Task EnqueueDocumentAsync(DocumentIndexingJobArgs args);

    Task EnqueueVideoAsync(VideoIndexingJobArgs args);
}
