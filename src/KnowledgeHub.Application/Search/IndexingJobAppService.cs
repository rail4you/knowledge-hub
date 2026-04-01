using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.Search;

public class IndexingJobAppService : KnowledgeHubAppService, IIndexingJobAppService
{
    private readonly IRepository<DocumentIndexingJob, Guid> _jobRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IFileStorageService _fileStorageService;

    public IndexingJobAppService(
        IRepository<DocumentIndexingJob, Guid> jobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IBackgroundJobManager backgroundJobManager,
        IDocumentExtractionService documentExtractionService,
        IFileStorageService fileStorageService)
    {
        _jobRepository = jobRepository;
        _resourceRepository = resourceRepository;
        _backgroundJobManager = backgroundJobManager;
        _documentExtractionService = documentExtractionService;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResultDto<IndexingJobDto>> GetListAsync(GetIndexingJobsInput input)
    {
        var query = await _jobRepository.GetQueryableAsync();
        
        query = query.OrderByDescending(x => x.CreationTime);

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (input.ResourceId.HasValue)
        {
            query = query.Where(x => x.ResourceId == input.ResourceId.Value);
        }

        var totalCount = await _jobRepository.AsyncExecuter.CountAsync(query);
        
        query = query.Skip(input.SkipCount).Take(input.MaxResultCount);

        var jobs = await _jobRepository.AsyncExecuter.ToListAsync(query);
        var resourceIds = jobs.Select(x => x.ResourceId).Distinct().ToList();
        var resources = await _resourceRepository.GetListAsync(x => resourceIds.Contains(x.Id));
        var resourceDict = resources.ToDictionary(x => x.Id, x => x.Name);

        var items = jobs.Select(job => new IndexingJobDto
        {
            Id = job.Id,
            ResourceId = job.ResourceId,
            ResourceName = resourceDict.GetValueOrDefault(job.ResourceId, "Unknown"),
            ResourceVersionId = job.ResourceVersionId,
            Status = job.Status,
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage,
            TotalPages = job.TotalPages,
            ProcessedPages = job.ProcessedPages,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            RetryCount = job.RetryCount,
            NextRetryAt = job.NextRetryAt,
            CreationTime = job.CreationTime
        }).ToList();

        return new PagedResultDto<IndexingJobDto>(totalCount, items);
    }

    public async Task<IndexingJobDto> GetAsync(Guid id)
    {
        var job = await _jobRepository.GetAsync(id);
        var resource = await _resourceRepository.FindAsync(job.ResourceId);

        return new IndexingJobDto
        {
            Id = job.Id,
            ResourceId = job.ResourceId,
            ResourceName = resource?.Name ?? "Unknown",
            ResourceVersionId = job.ResourceVersionId,
            Status = job.Status,
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage,
            TotalPages = job.TotalPages,
            ProcessedPages = job.ProcessedPages,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            RetryCount = job.RetryCount,
            NextRetryAt = job.NextRetryAt,
            CreationTime = job.CreationTime
        };
    }

    public async Task<IndexingJobDto?> GetByResourceIdAsync(Guid resourceId)
    {
        var job = await _jobRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId);
        if (job == null) return null;

        var resource = await _resourceRepository.FindAsync(job.ResourceId);

        return new IndexingJobDto
        {
            Id = job.Id,
            ResourceId = job.ResourceId,
            ResourceName = resource?.Name ?? "Unknown",
            ResourceVersionId = job.ResourceVersionId,
            Status = job.Status,
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage,
            TotalPages = job.TotalPages,
            ProcessedPages = job.ProcessedPages,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            RetryCount = job.RetryCount,
            NextRetryAt = job.NextRetryAt,
            CreationTime = job.CreationTime
        };
    }

    public async Task<IndexingJobDto> CreateAsync(CreateIndexingJobInput input)
    {
        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        
        var existingJob = await _jobRepository.FirstOrDefaultAsync(x => 
            x.ResourceId == input.ResourceId && 
            x.Status != IndexingJobStatus.Completed && 
            x.Status != IndexingJobStatus.Failed && 
            x.Status != IndexingJobStatus.Cancelled);

        if (existingJob != null)
        {
            throw new UserFriendlyException("A pending or in-progress indexing job already exists for this resource.");
        }

        var job = new DocumentIndexingJob
        {
            ResourceId = input.ResourceId,
            ResourceVersionId = input.ResourceVersionId,
            Status = IndexingJobStatus.Pending,
            TenantId = CurrentTenant.Id
        };

        await _jobRepository.InsertAsync(job);

        await _backgroundJobManager.EnqueueAsync(new DocumentIndexingJobArgs
        {
            JobId = job.Id,
            ResourceId = input.ResourceId,
            FilePath = resource.FilePath,
            TenantId = CurrentTenant.Id
        });

        return await GetAsync(job.Id);
    }

    public async Task<string> TestExecuteJobAsync(Guid id)
    {
        var job = await _jobRepository.GetAsync(id);
        var resource = await _resourceRepository.GetAsync(job.ResourceId);
        
        if (resource == null)
        {
            return "Resource not found";
        }
        
        if (string.IsNullOrEmpty(resource.FilePath))
        {
            return "Resource has no file path";
        }
        
        var fullPath = Path.Combine(_fileStorageService.RootPath, resource.FilePath);
        
        if (!File.Exists(fullPath))
        {
            return $"File not found: {fullPath}";
        }
        
        var parseResult = await _documentExtractionService.ExtractPagesAsync(job.ResourceId);
        return $"Successfully extracted {parseResult.Count} pages";
    }

    public async Task RetryAsync(Guid id)
    {
        var job = await _jobRepository.GetAsync(id);
        
        if (job.Status != IndexingJobStatus.Failed && job.Status != IndexingJobStatus.Pending)
        {
            throw new UserFriendlyException("Only failed or pending jobs can be retried.");
        }

        var resource = await _resourceRepository.GetAsync(job.ResourceId);

        job.Status = IndexingJobStatus.Pending;
        job.ErrorMessage = null;
        job.RetryCount = 0;
        job.NextRetryAt = null;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.Progress = 0;
        job.ProcessedPages = null;

        await _jobRepository.UpdateAsync(job);

        await _backgroundJobManager.EnqueueAsync(new DocumentIndexingJobArgs
        {
            JobId = job.Id,
            ResourceId = job.ResourceId,
            FilePath = resource.FilePath,
            TenantId = CurrentTenant.Id
        });
    }

    public async Task CancelAsync(Guid id)
    {
        var job = await _jobRepository.GetAsync(id);
        
        if (job.Status == IndexingJobStatus.Completed)
        {
            throw new UserFriendlyException("Cannot cancel a completed job.");
        }

        job.Status = IndexingJobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        await _jobRepository.UpdateAsync(job);
    }

    public async Task RetryAllFailedAsync()
    {
        var failedJobs = await _jobRepository.GetListAsync(x => x.Status == IndexingJobStatus.Failed);
        
        foreach (var job in failedJobs)
        {
            try
            {
                await RetryAsync(job.Id);
            }
            catch (Exception)
            {
            }
        }
    }

    public async Task<TestParseResultDto> TestParseAsync(Guid resourceId)
    {
        var resource = await _resourceRepository.GetAsync(resourceId);
        
        if (string.IsNullOrEmpty(resource.FilePath))
        {
            throw new UserFriendlyException("Resource has no file path");
        }

        var fullPath = Path.Combine(_fileStorageService.RootPath, resource.FilePath);
        
        if (!File.Exists(fullPath))
        {
            throw new UserFriendlyException($"File not found: {fullPath}");
        }

        try
        {
            var result = await _documentExtractionService.ExtractPagesAsync(resource.Id);

            return new TestParseResultDto
            {
                Success = true,
                PageCount = result.Count,
                FirstPagePreview = result.FirstOrDefault()?.Content?.Substring(0, Math.Min(500, result.FirstOrDefault()?.Content?.Length ?? 0))
            };
        }
        catch (Exception ex)
        {
            return new TestParseResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task TriggerAsync(Guid id)
    {
        var job = await _jobRepository.GetAsync(id);
        
        var resource = await _resourceRepository.GetAsync(job.ResourceId);

        var args = new DocumentIndexingJobArgs
        {
            JobId = job.Id,
            ResourceId = job.ResourceId,
            FilePath = resource.FilePath,
            TenantId = CurrentTenant.Id
        };

        var backgroundJob = ServiceProvider.GetRequiredService<DocumentIndexingBackgroundJob>();
        await backgroundJob.ExecuteAsync(args);
    }
}
