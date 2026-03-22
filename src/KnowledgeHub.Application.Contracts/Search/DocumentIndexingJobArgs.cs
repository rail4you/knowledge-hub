using System;
using Volo.Abp.BackgroundJobs;

namespace KnowledgeHub.Application.Contracts.Search;

public class DocumentIndexingJobArgs
{
    public Guid JobId { get; set; }
    public Guid ResourceId { get; set; }
    public string? FilePath { get; set; }
    public Guid? TenantId { get; set; }
    public bool IsRetry { get; set; }
}
