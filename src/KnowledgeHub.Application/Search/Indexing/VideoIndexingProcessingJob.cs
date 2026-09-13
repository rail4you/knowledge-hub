using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Search;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Search.Indexing;

/// <summary>
/// Hangfire 执行的视频索引任务：转调 <see cref="VideoIndexingBackgroundJob"/> 的既有逻辑。
/// 通过 <see cref="IIndexingJobQueue"/> 入队到 "indexing" 队列（PostgreSQL 持久化）。
/// </summary>
public class VideoIndexingProcessingJob : ITransientDependency
{
    private readonly VideoIndexingBackgroundJob _inner;

    public VideoIndexingProcessingJob(VideoIndexingBackgroundJob inner)
    {
        _inner = inner;
    }

    public Task ExecuteAsync(VideoIndexingJobArgs args)
    {
        return _inner.ExecuteAsync(args);
    }
}
