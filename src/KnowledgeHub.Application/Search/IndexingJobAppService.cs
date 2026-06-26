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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

[IgnoreAntiforgeryToken]
public class IndexingJobAppService : KnowledgeHubAppService, IIndexingJobAppService
{
    private readonly IRepository<DocumentIndexingJob, Guid> _jobRepository;
    private readonly IRepository<VideoIndexingJob, Guid> _videoJobRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IFileStorageService _fileStorageService;

    public IndexingJobAppService(
        IRepository<DocumentIndexingJob, Guid> jobRepository,
        IRepository<VideoIndexingJob, Guid> videoJobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IBackgroundJobManager backgroundJobManager,
        IDocumentExtractionService documentExtractionService,
        IFileStorageService fileStorageService)
    {
        _jobRepository = jobRepository;
        _videoJobRepository = videoJobRepository;
        _resourceRepository = resourceRepository;
        _backgroundJobManager = backgroundJobManager;
        _documentExtractionService = documentExtractionService;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResultDto<IndexingJobDto>> GetListAsync(GetIndexingJobsInput input)
    {
        // Query document jobs
        var docQuery = await _jobRepository.GetQueryableAsync();
        docQuery = docQuery.OrderByDescending(x => x.CreationTime);

        if (input.Status.HasValue)
        {
            var statusValue = input.Status.Value;
            docQuery = docQuery.Where(x => x.Status == statusValue);
        }

        if (input.ResourceId.HasValue)
        {
            var rid = input.ResourceId.Value;
            docQuery = docQuery.Where(x => x.ResourceId == rid);
        }

        if (input.StartTime.HasValue)
        {
            var st = input.StartTime.Value;
            docQuery = docQuery.Where(x => x.CreationTime >= st);
        }

        if (input.EndTime.HasValue)
        {
            var et = input.EndTime.Value;
            docQuery = docQuery.Where(x => x.CreationTime <= et);
        }

        var docJobs = await _jobRepository.AsyncExecuter.ToListAsync(docQuery);

        // Query video jobs
        var vidQuery = await _videoJobRepository.GetQueryableAsync();
        vidQuery = vidQuery.OrderByDescending(x => x.CreationTime);

        if (input.Status.HasValue)
        {
            var statusValue = (int)input.Status.Value;
            vidQuery = vidQuery.Where(x => (int)x.Status == statusValue);
        }

        if (input.ResourceId.HasValue)
        {
            var rid = input.ResourceId.Value;
            vidQuery = vidQuery.Where(x => x.ResourceId == rid);
        }

        if (input.StartTime.HasValue)
        {
            var st = input.StartTime.Value;
            vidQuery = vidQuery.Where(x => x.CreationTime >= st);
        }

        if (input.EndTime.HasValue)
        {
            var et = input.EndTime.Value;
            vidQuery = vidQuery.Where(x => x.CreationTime <= et);
        }

        var vidJobs = await _videoJobRepository.AsyncExecuter.ToListAsync(vidQuery);

        // Merge and map
        var allResourceIds = docJobs.Select(x => x.ResourceId)
            .Concat(vidJobs.Select(x => x.ResourceId))
            .Distinct().ToList();
        var resources = await _resourceRepository.GetListAsync(x => allResourceIds.Contains(x.Id));
        var resourceDict = resources.ToDictionary(x => x.Id, x => x.Name);

        var allItems = new List<IndexingJobDto>();

        allItems.AddRange(docJobs.Select(job => new IndexingJobDto
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
            CreationTime = job.CreationTime,
            JobType = "document"
        }));

        allItems.AddRange(vidJobs.Select(job => new IndexingJobDto
        {
            Id = job.Id,
            ResourceId = job.ResourceId,
            ResourceName = resourceDict.GetValueOrDefault(job.ResourceId, "Unknown"),
            ResourceVersionId = job.ResourceVersionId,
            // Map VideoIndexingJobStatus.Analyzing(15) → IndexingJobStatus.Parsing(10) for frontend compatibility
            Status = MapVideoStatus(job.Status),
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage,
            TotalPages = null,
            ProcessedPages = null,
            TotalSegments = job.TotalEvents,
            ProcessedSegments = job.ProcessedEvents,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            RetryCount = job.RetryCount,
            NextRetryAt = job.NextRetryAt,
            CreationTime = job.CreationTime,
            JobType = "video"
        }));

        // Sort merged results by CreationTime descending
        allItems = allItems.OrderByDescending(x => x.CreationTime).ToList();

        var totalCount = allItems.Count;
        var pagedItems = allItems.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<IndexingJobDto>(totalCount, pagedItems);
    }

    private static IndexingJobStatus MapVideoStatus(VideoIndexingJobStatus status)
    {
        return status switch
        {
            VideoIndexingJobStatus.Pending => IndexingJobStatus.Pending,
            VideoIndexingJobStatus.Parsing => IndexingJobStatus.Parsing,
            VideoIndexingJobStatus.Analyzing => IndexingJobStatus.Parsing, // Map Analyzing(15) → Parsing(10)
            VideoIndexingJobStatus.Indexing => IndexingJobStatus.Indexing,
            VideoIndexingJobStatus.Completed => IndexingJobStatus.Completed,
            VideoIndexingJobStatus.Failed => IndexingJobStatus.Failed,
            VideoIndexingJobStatus.Cancelled => IndexingJobStatus.Cancelled,
            _ => IndexingJobStatus.Pending
        };
    }

    public async Task<IndexingJobDto> GetAsync(Guid id)
    {
        // Try document job first
        var docJob = await _jobRepository.FindAsync(id);
        if (docJob != null)
        {
            var resource = await _resourceRepository.FindAsync(docJob.ResourceId);
            return new IndexingJobDto
            {
                Id = docJob.Id,
                ResourceId = docJob.ResourceId,
                ResourceName = resource?.Name ?? "Unknown",
                ResourceVersionId = docJob.ResourceVersionId,
                Status = docJob.Status,
                Progress = docJob.Progress,
                ErrorMessage = docJob.ErrorMessage,
                TotalPages = docJob.TotalPages,
                ProcessedPages = docJob.ProcessedPages,
                StartedAt = docJob.StartedAt,
                CompletedAt = docJob.CompletedAt,
                RetryCount = docJob.RetryCount,
                NextRetryAt = docJob.NextRetryAt,
                CreationTime = docJob.CreationTime,
                JobType = "document"
            };
        }

        // Try video job
        var vidJob = await _videoJobRepository.FindAsync(id);
        if (vidJob != null)
        {
            var resource = await _resourceRepository.FindAsync(vidJob.ResourceId);
            return new IndexingJobDto
            {
                Id = vidJob.Id,
                ResourceId = vidJob.ResourceId,
                ResourceName = resource?.Name ?? "Unknown",
                ResourceVersionId = vidJob.ResourceVersionId,
                Status = MapVideoStatus(vidJob.Status),
                Progress = vidJob.Progress,
                ErrorMessage = vidJob.ErrorMessage,
                TotalSegments = vidJob.TotalEvents,
                ProcessedSegments = vidJob.ProcessedEvents,
                StartedAt = vidJob.StartedAt,
                CompletedAt = vidJob.CompletedAt,
                RetryCount = vidJob.RetryCount,
                NextRetryAt = vidJob.NextRetryAt,
                CreationTime = vidJob.CreationTime,
                JobType = "video"
            };
        }

        throw new EntityNotFoundException(typeof(DocumentIndexingJob), id);
    }

    public async Task<IndexingJobDto?> GetByResourceIdAsync(Guid resourceId)
    {
        // Try document job first
        var job = await _jobRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId);
        if (job != null)
        {
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
                CreationTime = job.CreationTime,
                JobType = "document"
            };
        }

        // Try video job
        var vidJob = await _videoJobRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId);
        if (vidJob != null)
        {
            var resource = await _resourceRepository.FindAsync(vidJob.ResourceId);
            return new IndexingJobDto
            {
                Id = vidJob.Id,
                ResourceId = vidJob.ResourceId,
                ResourceName = resource?.Name ?? "Unknown",
                ResourceVersionId = vidJob.ResourceVersionId,
                Status = MapVideoStatus(vidJob.Status),
                Progress = vidJob.Progress,
                ErrorMessage = vidJob.ErrorMessage,
                TotalSegments = vidJob.TotalEvents,
                ProcessedSegments = vidJob.ProcessedEvents,
                StartedAt = vidJob.StartedAt,
                CompletedAt = vidJob.CompletedAt,
                RetryCount = vidJob.RetryCount,
                NextRetryAt = vidJob.NextRetryAt,
                CreationTime = vidJob.CreationTime,
                JobType = "video"
            };
        }

        return null;
    }

    public async Task<IndexingJobDto> CreateAsync(CreateIndexingJobInput input)
    {
        Resource? resource;
        // 先尝试不限制租户查询（支持跨租户创建索引任务）
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.FindAsync(input.ResourceId);
        }
        
        if (resource == null)
        {
            throw new UserFriendlyException($"资源不存在: {input.ResourceId}");
        }
        
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

        return new IndexingJobDto
        {
            Id = job.Id,
            ResourceId = job.ResourceId,
            ResourceName = resource.Name,
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
            CreationTime = job.CreationTime,
            JobType = "document"
        };
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
        // Try document job first
        var docJob = await _jobRepository.FindAsync(id);
        if (docJob != null)
        {
            if (docJob.Status != IndexingJobStatus.Failed && docJob.Status != IndexingJobStatus.Pending && docJob.Status != IndexingJobStatus.Completed)
            {
                throw new UserFriendlyException("Only failed, pending, or completed jobs can be retried.");
            }

            Resource? resource;
            using (DataFilter.Disable<IMultiTenant>())
            {
                resource = await _resourceRepository.GetAsync(docJob.ResourceId);
            }

            docJob.Status = IndexingJobStatus.Pending;
            docJob.ErrorMessage = null;
            docJob.RetryCount = 0;
            docJob.NextRetryAt = null;
            docJob.StartedAt = null;
            docJob.CompletedAt = null;
            docJob.Progress = 0;
            docJob.ProcessedPages = null;

            await _jobRepository.UpdateAsync(docJob);

            await _backgroundJobManager.EnqueueAsync(new DocumentIndexingJobArgs
            {
                JobId = docJob.Id,
                ResourceId = docJob.ResourceId,
                FilePath = resource.FilePath,
                TenantId = CurrentTenant.Id
            });
            return;
        }

        // Try video job
        var vidJob = await _videoJobRepository.FindAsync(id);
        if (vidJob != null)
        {
            if (vidJob.Status != VideoIndexingJobStatus.Failed && vidJob.Status != VideoIndexingJobStatus.Pending && vidJob.Status != VideoIndexingJobStatus.Completed)
            {
                throw new UserFriendlyException("Only failed, pending, or completed jobs can be retried.");
            }

            Resource? vidResource;
            using (DataFilter.Disable<IMultiTenant>())
            {
                vidResource = await _resourceRepository.GetAsync(vidJob.ResourceId);
            }

            vidJob.Status = VideoIndexingJobStatus.Pending;
            vidJob.ErrorMessage = null;
            vidJob.RetryCount = 0;
            vidJob.NextRetryAt = null;
            vidJob.StartedAt = null;
            vidJob.CompletedAt = null;
            vidJob.Progress = 0;
            vidJob.ProcessedEvents = null;

            await _videoJobRepository.UpdateAsync(vidJob);

            await _backgroundJobManager.EnqueueAsync(new VideoIndexingJobArgs
            {
                JobId = vidJob.Id,
                ResourceId = vidJob.ResourceId,
                FilePath = vidResource.FilePath,
                TenantId = CurrentTenant.Id
            });
            return;
        }

        throw new EntityNotFoundException(typeof(DocumentIndexingJob), id);
    }

    public async Task CancelAsync(Guid id)
    {
        // Try document job first
        var docJob = await _jobRepository.FindAsync(id);
        if (docJob != null)
        {
            if (docJob.Status == IndexingJobStatus.Completed)
            {
                throw new UserFriendlyException("Cannot cancel a completed job.");
            }

            docJob.Status = IndexingJobStatus.Cancelled;
            docJob.CompletedAt = DateTime.UtcNow;
            await _jobRepository.UpdateAsync(docJob);
            return;
        }

        // Try video job
        var vidJob = await _videoJobRepository.FindAsync(id);
        if (vidJob != null)
        {
            if (vidJob.Status == VideoIndexingJobStatus.Completed)
            {
                throw new UserFriendlyException("Cannot cancel a completed job.");
            }

            vidJob.Status = VideoIndexingJobStatus.Cancelled;
            vidJob.CompletedAt = DateTime.UtcNow;
            await _videoJobRepository.UpdateAsync(vidJob);
            return;
        }

        throw new EntityNotFoundException(typeof(DocumentIndexingJob), id);
    }

    public async Task RetryAllFailedAsync()
    {
        var failedDocJobs = await _jobRepository.GetListAsync(x => x.Status == IndexingJobStatus.Failed);
        foreach (var job in failedDocJobs)
        {
            try { await RetryAsync(job.Id); } catch { }
        }

        var failedVidJobs = await _videoJobRepository.GetListAsync(x => x.Status == VideoIndexingJobStatus.Failed);
        foreach (var job in failedVidJobs)
        {
            try { await RetryAsync(job.Id); } catch { }
        }
    }

    public async Task<TestParseResultDto> TestParseAsync(Guid resourceId)
    {
        Resource? resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.FindAsync(resourceId);
        }
        
        if (resource == null)
        {
            throw new UserFriendlyException($"资源不存在: {resourceId}");
        }
        
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
        
        Resource? resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.GetAsync(job.ResourceId);
        }

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
