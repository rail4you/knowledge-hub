using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace KnowledgeHub.Application.Search;

public class DocumentIndexingBackgroundJob : IAsyncBackgroundJob<DocumentIndexingJobArgs>, ITransientDependency
{
    private readonly IRepository<DocumentIndexingJob, Guid> _jobRepository;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IMeiliSearchService _meiliSearchService;
    private readonly ILogger<DocumentIndexingBackgroundJob> _logger;

    public DocumentIndexingBackgroundJob(
        IRepository<DocumentIndexingJob, Guid> jobRepository,
        IRepository<PageContent, Guid> pageContentRepository,
        IRepository<Resource, Guid> resourceRepository,
        IDocumentExtractionService documentExtractionService,
        IFileStorageService fileStorageService,
        IUnitOfWorkManager unitOfWorkManager,
        IMeiliSearchService meiliSearchService,
        ILogger<DocumentIndexingBackgroundJob> logger)
    {
        _jobRepository = jobRepository;
        _pageContentRepository = pageContentRepository;
        _resourceRepository = resourceRepository;
        _documentExtractionService = documentExtractionService;
        _fileStorageService = fileStorageService;
        _unitOfWorkManager = unitOfWorkManager;
        _meiliSearchService = meiliSearchService;
        _logger = logger;
        _logger.LogInformation("DocumentIndexingBackgroundJob CONSTRUCTOR called");
    }

    public async Task ExecuteAsync(DocumentIndexingJobArgs args)
    {
        _logger.LogInformation("DocumentIndexingBackgroundJob.ExecuteAsync STARTED for job {JobId}, resource {ResourceId}", args.JobId, args.ResourceId);
        _logger.LogInformation("Starting indexing job {JobId} for resource {ResourceId}", args.JobId, args.ResourceId);

        try
        {
            await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Parsing, progress: 5);
            await ExecuteJobAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing job {JobId} failed: {Error}", args.JobId, ex.Message);
            await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Failed, errorMessage: ex.Message);
        }
    }

    private async Task ExecuteJobAsync(DocumentIndexingJobArgs args)
    {
        var resource = await _resourceRepository.FindAsync(args.ResourceId);
        if (resource == null)
        {
            throw new Exception($"Resource not found: {args.ResourceId}");
        }

        if (string.IsNullOrEmpty(resource.FilePath))
        {
            throw new Exception("Resource has no file path");
        }

        var fullPath = Path.Combine(_fileStorageService.RootPath, resource.FilePath);
        
        if (!File.Exists(fullPath))
        {
            throw new Exception($"File not found: {fullPath}");
        }

        await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Parsing, progress: 10);
        _logger.LogInformation("Parsing document: {ResourceId}", args.ResourceId);

        var pages = await _documentExtractionService.ExtractPagesAsync(args.ResourceId);
        
        await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Indexing, progress: 30, totalPages: pages.Count);
        _logger.LogInformation("Parsed {PageCount} pages, now indexing", pages.Count);

        await DeleteExistingPagesAsync(args.ResourceId);

        var pageContents = new List<PageContent>();
        for (int i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var pageContent = new PageContent
            {
                ResourceId = args.ResourceId,
                PageNumber = page.PageNumber,
                Content = page.Content ?? string.Empty,
                TenantId = args.TenantId
            };
            pageContents.Add(pageContent);

            var progress = 30 + (int)((i + 1) / (double)pages.Count * 50);
            await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Indexing, progress: progress, processedPages: i + 1);
        }

        await _pageContentRepository.InsertManyAsync(pageContents);

        await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Indexing, progress: 85);
        _logger.LogInformation("Saved page content to database for resource {ResourceId}", args.ResourceId);

        var pageDtos = pageContents.Select(pc => new PageContentDto
        {
            PageNumber = pc.PageNumber,
            Content = pc.Content,
            Title = null
        }).ToList();

        try
        {
            await _meiliSearchService.IndexDocumentFromPagesAsync(args.ResourceId, pageDtos);
        }
        catch (Exception meiliEx)
        {
            _logger.LogWarning(meiliEx, "Meilisearch indexing failed for resource {ResourceId}", args.ResourceId);
            // Non-fatal: PageContent is already saved, can be re-indexed later
        }

        await UpdateJobStatusAsync(args.JobId, IndexingJobStatus.Completed, progress: 100);
        _logger.LogInformation("Indexing completed for resource {ResourceId}", args.ResourceId);
    }

    private async Task DeleteExistingPagesAsync(Guid resourceId)
    {
        var existingPages = await _pageContentRepository.GetListAsync(x => x.ResourceId == resourceId);
        if (existingPages.Any())
        {
            await _pageContentRepository.DeleteManyAsync(existingPages);
        }
    }

    private async Task UpdateJobStatusAsync(
        Guid jobId,
        IndexingJobStatus status,
        int? progress = null,
        string? errorMessage = null,
        int? totalPages = null,
        int? processedPages = null)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        
        var job = await _jobRepository.FindAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("Job not found when updating status: {JobId}", jobId);
            await uow.CompleteAsync();
            return;
        }

        job.Status = status;
        
        if (progress.HasValue)
            job.Progress = progress.Value;
            
        if ((status == IndexingJobStatus.Parsing || status == IndexingJobStatus.Indexing) && !job.StartedAt.HasValue)
            job.StartedAt = DateTime.UtcNow;
            
        if (status == IndexingJobStatus.Completed || status == IndexingJobStatus.Failed)
            job.CompletedAt = DateTime.UtcNow;
            
        if (!string.IsNullOrEmpty(errorMessage))
            job.ErrorMessage = errorMessage;
            
        if (totalPages.HasValue)
            job.TotalPages = totalPages.Value;
            
        if (processedPages.HasValue)
            job.ProcessedPages = processedPages.Value;

        await _jobRepository.UpdateAsync(job);
        await uow.CompleteAsync();
    }
}
