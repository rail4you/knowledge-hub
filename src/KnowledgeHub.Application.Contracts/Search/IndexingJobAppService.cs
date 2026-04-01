using System;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IIndexingJobAppService : IApplicationService
{
    Task<PagedResultDto<IndexingJobDto>> GetListAsync(GetIndexingJobsInput input);
    Task<IndexingJobDto?> GetByResourceIdAsync(Guid resourceId);
    Task<IndexingJobDto> GetAsync(Guid id);
    Task<IndexingJobDto> CreateAsync(CreateIndexingJobInput input);
    Task RetryAsync(Guid id);
    Task<string> TestExecuteJobAsync(Guid id);
    Task CancelAsync(Guid id);
    Task RetryAllFailedAsync();
    Task<TestParseResultDto> TestParseAsync(Guid resourceId);
    Task TriggerAsync(Guid id);
}

public class TestParseResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PageCount { get; set; }
    public string? FirstPagePreview { get; set; }
}

public class GetIndexingJobsInput : PagedAndSortedResultRequestDto
{
    public Guid? ResourceId { get; set; }
    public IndexingJobStatus? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class CreateIndexingJobInput
{
    public Guid ResourceId { get; set; }
    public Guid? ResourceVersionId { get; set; }
}
