using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Search;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Search.Indexing;

/// <summary>
/// Hangfire 执行的文档索引任务：转调 <see cref="DocumentIndexingBackgroundJob"/> 的既有逻辑。
/// 通过 <see cref="IIndexingJobQueue"/> 入队到 "indexing" 队列（PostgreSQL 持久化）。
/// </summary>
public class DocumentIndexingProcessingJob : ITransientDependency
{
    private readonly DocumentIndexingBackgroundJob _inner;

    public DocumentIndexingProcessingJob(DocumentIndexingBackgroundJob inner)
    {
        _inner = inner;
    }

    public Task ExecuteAsync(DocumentIndexingJobArgs args)
    {
        return _inner.ExecuteAsync(args);
    }
}
