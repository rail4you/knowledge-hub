using System;
using System.IO;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Configuration;
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
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoIndexingBackgroundJob> _logger;

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".qt" };

    public VideoIndexingBackgroundJob(
        IRepository<VideoIndexingJob, Guid> jobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        IVideoAnalysisAppService videoAnalysisAppService,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        ILogger<VideoIndexingBackgroundJob> logger)
    {
        _jobRepository = jobRepository;
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _videoAnalysisAppService = videoAnalysisAppService;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(VideoIndexingJobArgs args)
    {
        _logger.LogInformation("VideoIndexingBackgroundJob.ExecuteAsync STARTED for job {JobId}, resource {ResourceId}, tenant {TenantId}", args.JobId, args.ResourceId, args.TenantId);

        // Set tenant context for multi-tenant resource retrieval
        using (_currentTenant.Change(args.TenantId))
        {
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

        var analysisRequest = BuildVideoAnalysisRequest(videoPath, videoUrl, resource.FilePath);

        _logger.LogInformation("Video analysis request: FilePath={FilePath}, VideoUrl={VideoUrl}",
            analysisRequest.FilePath,
            analysisRequest.VideoUrl != null && analysisRequest.VideoUrl.Length > 100
                ? analysisRequest.VideoUrl[..100] + "..."
                : analysisRequest.VideoUrl);

        var analysisResult = await _videoAnalysisAppService.AnalyzeVideoTimelineAsync(analysisRequest);

        await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Indexing, progress: 70, totalEvents: analysisResult.Events.Count, processedEvents: 0);
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
            _logger.LogError(meiliEx, "Meilisearch indexing failed for resource {ResourceId}", args.ResourceId);
            await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Failed, errorMessage: $"Meilisearch索引失败: {meiliEx.Message}");
            throw;
        }

        await UpdateJobStatusAsync(args.JobId, VideoIndexingJobStatus.Completed, progress: 100, processedEvents: analysisResult.Events.Count);
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

    /// <summary>
    /// Build the video analysis request, preferring the absolute public URL when available
    /// (for remote/production servers) and falling back to local base64 loading for localhost.
    ///
    /// For production, also pre-compresses large videos (>= 30MB) to ~480p using ffmpeg,
    /// because Qwen VL API (on Aliyun) times out downloading multi-hundred-MB videos
    /// over cross-cloud links. The compressed file is written under uploads/_tmp/ so
    /// nginx /uploads/ proxy serves it directly to Qwen.
    /// </summary>
    private VideoAnalysisRequestDto BuildVideoAnalysisRequest(string videoPath, string? videoUrl, string? resourceFilePath)
    {
        var selfUrl = _configuration["App:SelfUrl"] ?? "";

        if (!string.IsNullOrEmpty(videoUrl)
            && !string.IsNullOrEmpty(selfUrl)
            && !IsLocalhostUrl(selfUrl))
        {
            string urlToUse = videoUrl;

            // 大文件预压缩：Qwen 在阿里云，跨云下载大文件容易超时
            var compressedPath = TryCompressForQwen(videoPath);
            if (compressedPath != null)
            {
                var compressedFileName = Path.GetFileName(compressedPath);
                urlToUse = $"/uploads/_tmp/{compressedFileName}";
                _logger.LogInformation("Using compressed video URL: {Url} (original: {Size:F2} MB -> compressed: {CompSize:F2} MB)",
                    $"{selfUrl.TrimEnd('/')}{urlToUse}",
                    new FileInfo(videoPath).Length / 1024.0 / 1024.0,
                    new FileInfo(compressedPath).Length / 1024.0 / 1024.0);
            }
            else
            {
                // 视频不大或者压缩失败，直接用原文件 URL
                _logger.LogInformation("Using public video URL: {Url}", $"{selfUrl.TrimEnd('/')}{videoUrl}");
            }

            return new VideoAnalysisRequestDto
            {
                FilePath = null,  // Don't use local file path
                VideoUrl = $"{selfUrl.TrimEnd('/')}{urlToUse}"
            };
        }

        // Localhost or no URL → fallback to local file path (base64 loading)
        _logger.LogInformation("Using local video file path (base64): {Path}", videoPath);
        return new VideoAnalysisRequestDto
        {
            FilePath = videoPath,
            VideoUrl = null
        };
    }

    /// <summary>
    /// 大文件压缩：阈值 30MB；压缩到 480p H.264，输出到 /app/uploads/_tmp/。
    /// 同源文件多次索引会复用同一压缩产物（按源 mtime 判断）。
    /// 失败时返回 null，调用方回退到原文件 URL。
    /// </summary>
    private string? TryCompressForQwen(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
        {
            return null;
        }

        var fileInfo = new FileInfo(videoPath);
        // 仅压缩 > 30MB 的视频；小文件直接走原 URL 即可
        if (fileInfo.Length <= 30 * 1024 * 1024)
        {
            return null;
        }

        try
        {
            // 输出路径：/app/uploads/_tmp/<safe_name>.compressed.mp4
            // 用 sha1(源路径) 做文件名，避免中文/空格/特殊字符在文件系统/URL 上出问题
            var tmpDir = Path.Combine(_fileStorageService.RootPath, "_tmp");
            Directory.CreateDirectory(tmpDir);

            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(videoPath));
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var compressedPath = Path.Combine(tmpDir, $"{hashHex}.compressed.mp4");

            // 缓存：如果压缩产物已存在且比源文件新，直接复用
            if (File.Exists(compressedPath) && File.GetLastWriteTime(compressedPath) >= fileInfo.LastWriteTime)
            {
                return compressedPath;
            }

            _logger.LogInformation("Compressing video for Qwen: {Source} ({Size:F2} MB) -> {Target}",
                videoPath, fileInfo.Length / 1024.0 / 1024.0, compressedPath);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                // 480p + crf 32：保留下采样后清晰度足够 Qwen 1fps 抽帧理解内容，文件能压到几 MB~几十 MB
                Arguments = $"-i \"{videoPath}\" -vf \"scale=-2:480\" -vcodec libx264 -crf 32 -preset fast -acodec aac -b:a 64k -movflags +faststart -y \"{compressedPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Failed to start ffmpeg for compression, falling back to original URL");
                return null;
            }

            // 给压缩一些时间；视频很大时可能耗时数分钟
            if (!process.WaitForExit(15 * 60 * 1000))
            {
                try { process.Kill(true); } catch { }
                _logger.LogWarning("ffmpeg compression timed out after 15min, falling back to original URL");
                return null;
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                _logger.LogWarning("ffmpeg compression failed (exit={Code}): {Err}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(compressedPath))
            {
                _logger.LogWarning("ffmpeg exit 0 but compressed file missing, falling back");
                return null;
            }

            return compressedPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Compression exception, falling back to original URL");
            return null;
        }
    }

    private static bool IsLocalhostUrl(string url)
    {
        return url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || url.Contains("127.0.0.1")
            || url.Contains("::1");
    }

    public static bool IsVideoFile(string? fileExtension)
    {
        if (string.IsNullOrEmpty(fileExtension))
            return false;

        return VideoExtensions.Contains(fileExtension.ToLowerInvariant());
    }
}