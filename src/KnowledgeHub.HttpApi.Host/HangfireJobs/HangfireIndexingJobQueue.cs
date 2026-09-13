using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Search.Indexing;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 实现的文档/视频索引队列（PostgreSQL 持久化）。
/// 入队到 "indexing" 专用队列，与 default/conversion/ai/media 隔离；重启不丢任务。
/// </summary>
public class HangfireIndexingJobQueue : IIndexingJobQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireIndexingJobQueue(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task EnqueueDocumentAsync(DocumentIndexingJobArgs args)
    {
        _backgroundJobClient.Enqueue<DocumentIndexingProcessingJob>(
            "indexing",
            job => job.ExecuteAsync(args));
        return Task.CompletedTask;
    }

    public Task EnqueueVideoAsync(VideoIndexingJobArgs args)
    {
        _backgroundJobClient.Enqueue<VideoIndexingProcessingJob>(
            "indexing",
            job => job.ExecuteAsync(args));
        return Task.CompletedTask;
    }
}
