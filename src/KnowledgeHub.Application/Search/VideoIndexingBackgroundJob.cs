using System;
using System.IO;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Application.Search;

public class VideoIndexingBackgroundJob : IAsyncBackgroundJob<VideoIndexingJobArgs>, ITransientDependency
{
    private readonly IRepository<VideoIndexingJob, Guid> _jobRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IVideoAnalysisAppService _videoAnalysisAppService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<VideoIndexingBackgroundJob> _logger;

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".qt" };

    public VideoIndexingBackgroundJob(
        IRepository<VideoIndexingJob, Guid> jobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        IVideoAnalysisAppService videoAnalysisAppService,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<VideoIndexingBackgroundJob> logger)
    {
        _jobRepository = jobRepository;
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _videoAnalysisAppService = videoAnalysisAppService;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task ExecuteAsync(VideoIndexingJobArgs args)
    {
        _logger.LogInformation("VideoIndexingBackgroundJob.ExecuteAsync STARTED for job {JobId}, resource {ResourceId}", args.JobId, args.ResourceId);

        try
        {
            await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Parsing, progress: 5);
            await ExecuteJobAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video indexing job {JobId} failed: {Error}", args.JobId, ex.Message);
            await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Failed, errorMessage: ex.Message);
        }
    }

    private async Task ExecuteJobAsync(VideoIndexingJobArgs args)
    {
        var resource = await _resourceRepository.FindAsync(args.ResourceId);
        if (resource == null)
        {
            throw new Exception($"Resource not found: {args.ResourceId}");
        }

        string videoPath;
        string? videoUrl = null;

        if (!string.IsNullOrEmpty(resource.FilePath))
        {
            videoPath = Path.Combine(_fileStorageService.RootPath, resource.FilePath);

            if (!File.Exists(videoPath))
            {
                throw new Exception($"File not found: {videoPath}");
            }
            videoUrl = _fileStorageService.GetFileUrl(resource.FilePath);
        }
        else if (!string.IsNullOrEmpty(args.VideoUrl))
        {
            videoPath = args.VideoUrl;
            videoUrl = args.VideoUrl;
        }
        else
        {
            throw new Exception("Resource has no file path or video URL");
        }

        await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Analyzing, progress: 20);
        _logger.LogInformation("Analyzing video: {ResourceId}", args.ResourceId);

        var analysisRequest = new VideoAnalysisRequestDto
        {
            FilePath = videoPath,
            VideoUrl = videoUrl
        };

        var analysisResult = await _videoAnalysisAppService.AnalyzeVideoTimelineAsync(analysisRequest);

        await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Indexing, progress: 70, totalEvents: analysisResult.Events.Count);
        _logger.LogInformation("Analyzed {EventCount} timeline events, saving to Meilisearch", analysisResult.Events.Count);

        try
        {
            await _videoAnalysisAppService.SaveVideoTimelineToMeiliSearchAsync(
                resource.Id,
                resource.Name ?? "Untitled Video",
                videoUrl ?? "",
                analysisResult);

            _logger.LogInformation("Video timeline saved to Meilisearch for resource {ResourceId}", args.ResourceId);
        }
        catch (Exception meiliEx)
        {
            _logger.LogWarning(meiliEx, "Meilisearch indexing failed for resource {ResourceId}", args.ResourceId);
        }

        await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Completed, progress: 100);
        _logger.LogInformation("Video indexing completed for resource {ResourceId}", args.ResourceId);
    }

    private async Task UpdateJobStatusAsync(
        Guid jobId,
        VideoIndexingJobStatus status,
        int? progress = null,
        string? errorMessage = null,
        int? totalEvents = null,
        int? processedEvents = null)
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

        if ((status == VideoIndexingJobStatus.Parsing || status == VideoIndexingJobStatus.Analyzing || status == VideoIndexingJobStatus.Indexing) && !job.StartedAt.HasValue)
            job.StartedAt = DateTime.UtcNow;

        if (status == VideoIndexingJobStatus.Completed || status == VideoIndexingJobStatus.Failed)
            job.CompletedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(errorMessage))
            job.ErrorMessage = errorMessage;

        if (totalEvents.HasValue)
            job.TotalEvents = totalEvents.Value;

        if (processedEvents.HasValue)
            job.ProcessedEvents = processedEvents.Value;

        await _jobRepository.UpdateAsync(job);
        await uow.CompleteAsync();
    }

    public static bool IsVideoFile(string? fileExtension)
    {
        if (string.IsNullOrEmpty(fileExtension))
            return false;

        return VideoExtensions.Contains(fileExtension.ToLowerInvariant());
    }
}