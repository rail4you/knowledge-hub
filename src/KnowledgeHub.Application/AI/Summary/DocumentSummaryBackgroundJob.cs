using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.AI.Summary;

/// <summary>
/// ABP 自动注册的 background job（IAsyncBackgroundJob&lt;T&gt; + ITransientDependency）。
/// 异常向上抛 → ABP 后台 Job 默认重试 3 次。
/// 不写 DocumentIndexingJob 状态表，避免污染前端索引任务页面。
/// </summary>
public class DocumentSummaryBackgroundJob
    : IAsyncBackgroundJob<DocumentSummaryGenerationJobArgs>, ITransientDependency
{
    private readonly IDocumentSummaryService _summaryService;
    private readonly ILogger<DocumentSummaryBackgroundJob> _logger;

    public DocumentSummaryBackgroundJob(
        IDocumentSummaryService summaryService,
        ILogger<DocumentSummaryBackgroundJob> logger)
    {
        _summaryService = summaryService;
        _logger = logger;
    }

    public async Task ExecuteAsync(DocumentSummaryGenerationJobArgs args)
    {
        _logger.LogInformation(
            "DocumentSummaryBackgroundJob STARTED for resource {ResourceId}, tenant {TenantId}",
            args.ResourceId, args.TenantId);

        await _summaryService.GenerateAndPersistAsync(args);

        _logger.LogInformation(
            "DocumentSummaryBackgroundJob COMPLETED for resource {ResourceId}",
            args.ResourceId);
    }
}