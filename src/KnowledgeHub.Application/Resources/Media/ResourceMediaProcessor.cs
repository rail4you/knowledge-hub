using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Resources.Thumbnails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowledgeHub.Resources.Media;

public enum MediaProcessStatus
{
    /// <summary>全部生成物就绪，或无生成物。</summary>
    Completed,
    /// <summary>部分生成物失败（其余可用）。</summary>
    PartialFailed,
    /// <summary>处理失败。</summary>
    Failed
}

public sealed record MediaProcessOutcome(MediaProcessStatus Status, string? Error)
{
    public static readonly MediaProcessOutcome Ok = new(MediaProcessStatus.Completed, null);
    public static MediaProcessOutcome Partial(string error) => new(MediaProcessStatus.PartialFailed, error);
    public static MediaProcessOutcome Fail(string error) => new(MediaProcessStatus.Failed, error);
}

/// <summary>
/// 资源媒体处理核心：按资源版本类型生成缩略图 / Office 预览 PDF，并登记 ResourceArtifact、
/// 更新 Resource.MediaStatus。不负责任务状态机（由 ResourceMediaProcessingJob 负责）。
/// </summary>
public class ResourceMediaProcessor : ITransientDependency
{
    private const int ThumbnailWidth = 400;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".avif"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".flv", ".mpeg", ".mpg"
    };

    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pptx", ".ppt", ".docx", ".doc", ".xlsx", ".xls"
    };

    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _versionRepository;
    private readonly IRepository<ResourceArtifact, Guid> _artifactRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IResourceThumbnailService _thumbnailService;
    private readonly IOfficeConversionService _officeConversionService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<ResourceMediaProcessor> _logger;

    public ResourceMediaProcessor(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> versionRepository,
        IRepository<ResourceArtifact, Guid> artifactRepository,
        IFileStorageService fileStorageService,
        IResourceThumbnailService thumbnailService,
        IOfficeConversionService officeConversionService,
        IGuidGenerator guidGenerator,
        IOptions<OfficeConversionOptions> options,
        ILogger<ResourceMediaProcessor> logger)
    {
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _artifactRepository = artifactRepository;
        _fileStorageService = fileStorageService;
        _thumbnailService = thumbnailService;
        _officeConversionService = officeConversionService;
        _guidGenerator = guidGenerator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MediaProcessOutcome> ProcessAsync(
        Guid resourceId,
        Guid? resourceVersionId,
        Func<int, string?, Task> reportProgress,
        CancellationToken ct)
    {
        var resource = await _resourceRepository.FindAsync(resourceId);
        if (resource == null)
        {
            return MediaProcessOutcome.Fail("资源不存在");
        }

        if (!string.IsNullOrEmpty(resource.FilePath))
        {
            // 兜底：资源级文件路径存在，但版本表可能未建版本（历史数据）
        }

        var version = await ResolveVersionAsync(resourceId, resourceVersionId);
        var (fullPath, versionKey) = ResolveSource(resource, version);
        if (fullPath == null)
        {
            // 无源文件（仅正文等），无需媒体处理
            resource.MediaStatus = ResourceMediaStatus.Ready;
            await _resourceRepository.UpdateAsync(resource);
            return MediaProcessOutcome.Ok;
        }

        var ext = Path.GetExtension(fullPath);
        var failures = new List<string>();
        var planned = 0;

        if (ImageExtensions.Contains(ext) || VideoExtensions.Contains(ext))
        {
            planned++;
            await reportProgress(30, "正在生成缩略图…");
            try
            {
                var abs = await _thumbnailService.GetOrCreateAsync(versionKey, fullPath, ThumbnailWidth, ct);
                if (abs != null)
                {
                    await UpsertArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                        $"w{ThumbnailWidth}", abs, "image/jpeg", ct);
                }
                else
                {
                    failures.Add("缩略图");
                    await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                        $"w{ThumbnailWidth}", "缩略图生成失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MediaProcessor] 缩略图失败 resource={ResourceId}", resourceId);
                failures.Add("缩略图");
                await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                    $"w{ThumbnailWidth}", "缩略图生成失败");
            }
        }

        if (OfficeExtensions.Contains(ext))
        {
            planned++;
            await reportProgress(60, "正在转换预览…");
            var sizeBytes = new FileInfo(fullPath).Length;
            if (sizeBytes > _options.MaxPreviewFileSizeBytes)
            {
                failures.Add("预览 PDF");
                await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.PreviewPdf,
                    "full", "文件过大，暂不生成在线预览");
            }
            else
            {
                try
                {
                    var pdfAbs = await _officeConversionService.ConvertToPdfAsync(
                        resourceId.ToString(), fullPath, "preview", ct);
                    await UpsertArtifactAsync(resource, version, ResourceArtifactKind.PreviewPdf,
                        "full", pdfAbs, "application/pdf", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MediaProcessor] 预览转换失败 resource={ResourceId}", resourceId);
                    failures.Add("预览 PDF");
                    await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.PreviewPdf,
                        "full", "文档转换失败");
                }
            }
        }

        await reportProgress(95, "收尾…");

        MediaProcessOutcome outcome;
        if (planned == 0 || failures.Count == 0)
        {
            resource.MediaStatus = ResourceMediaStatus.Ready;
            outcome = MediaProcessOutcome.Ok;
        }
        else if (failures.Count < planned)
        {
            resource.MediaStatus = ResourceMediaStatus.PartialFailed;
            outcome = MediaProcessOutcome.Partial($"部分生成物失败：{string.Join("、", failures)}");
        }
        else
        {
            resource.MediaStatus = ResourceMediaStatus.Failed;
            outcome = MediaProcessOutcome.Fail($"生成失败：{string.Join("、", failures)}");
        }

        await _resourceRepository.UpdateAsync(resource);
        return outcome;
    }

    private async Task<ResourceVersion?> ResolveVersionAsync(Guid resourceId, Guid? versionId)
    {
        if (versionId.HasValue)
        {
            var v = await _versionRepository.FindAsync(versionId.Value);
            if (v != null)
            {
                return v;
            }
        }

        return await _versionRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId && x.IsCurrentVersion)
            ?? await _versionRepository.FirstOrDefaultAsync(x => x.ResourceId == resourceId);
    }

    private (string? fullPath, string versionKey) ResolveSource(Resource resource, ResourceVersion? version)
    {
        var versionKey = (version?.Id ?? resource.Id).ToString();
        var filePath = version?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = resource.FilePath;
        }
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return (null, versionKey);
        }

        var fullPath = Path.Combine(_fileStorageService.RootPath, filePath);
        return File.Exists(fullPath) ? (fullPath, versionKey) : (null, versionKey);
    }

    private async Task UpsertArtifactAsync(
        Resource resource,
        ResourceVersion? version,
        ResourceArtifactKind kind,
        string variant,
        string absolutePath,
        string contentType,
        CancellationToken ct)
    {
        var versionId = version?.Id ?? resource.Id;
        var relative = Path.GetRelativePath(_fileStorageService.RootPath, absolutePath).Replace('\\', '/');
        var size = File.Exists(absolutePath) ? new FileInfo(absolutePath).Length : (long?)null;

        var existing = await _artifactRepository.FirstOrDefaultAsync(x =>
            x.ResourceId == resource.Id && x.ResourceVersionId == versionId && x.Kind == kind && x.Variant == variant);

        if (existing == null)
        {
            existing = new ResourceArtifact(_guidGenerator.Create(), resource.Id, versionId, kind, variant)
            {
                TenantId = resource.TenantId,
            };
            ApplyArtifact(existing, relative, contentType, size);
            await _artifactRepository.InsertAsync(existing);
        }
        else
        {
            ApplyArtifact(existing, relative, contentType, size);
            await _artifactRepository.UpdateAsync(existing);
        }
    }

    private async Task UpsertFailedArtifactAsync(
        Resource resource,
        ResourceVersion? version,
        ResourceArtifactKind kind,
        string variant,
        string error)
    {
        var versionId = version?.Id ?? resource.Id;
        var existing = await _artifactRepository.FirstOrDefaultAsync(x =>
            x.ResourceId == resource.Id && x.ResourceVersionId == versionId && x.Kind == kind && x.Variant == variant);
        var isNew = existing == null;
        existing ??= new ResourceArtifact(_guidGenerator.Create(), resource.Id, versionId, kind, variant)
        {
            TenantId = resource.TenantId,
        };
        existing.State = ResourceArtifactState.Failed;
        existing.ErrorMessage = error;
        existing.GeneratedAt = DateTime.UtcNow;
        if (isNew)
        {
            await _artifactRepository.InsertAsync(existing);
        }
        else
        {
            await _artifactRepository.UpdateAsync(existing);
        }
    }

    private static void ApplyArtifact(ResourceArtifact artifact, string relativePath, string contentType, long? size)
    {
        artifact.FilePath = relativePath;
        artifact.ContentType = contentType;
        artifact.SizeBytes = size;
        artifact.State = ResourceArtifactState.Ready;
        artifact.ErrorMessage = null;
        artifact.GeneratedAt = DateTime.UtcNow;
    }
}
