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
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

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
    /// <summary>本次实际处理的版本 Id（用于回写任务，保证产物与任务指向一致）。</summary>
    public Guid? ResourceVersionId { get; init; }

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
    private readonly IPdfPageRasterizer _pdfPageRasterizer;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<ResourceMediaProcessor> _logger;

    public ResourceMediaProcessor(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> versionRepository,
        IRepository<ResourceArtifact, Guid> artifactRepository,
        IFileStorageService fileStorageService,
        IResourceThumbnailService thumbnailService,
        IOfficeConversionService officeConversionService,
        IPdfPageRasterizer pdfPageRasterizer,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<OfficeConversionOptions> options,
        ILogger<ResourceMediaProcessor> logger)
    {
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _artifactRepository = artifactRepository;
        _fileStorageService = fileStorageService;
        _thumbnailService = thumbnailService;
        _officeConversionService = officeConversionService;
        _pdfPageRasterizer = pdfPageRasterizer;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
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

        var version = await ResolveVersionAsync(resource, resourceVersionId);
        var (fullPath, versionKey, pathConfigured) = ResolveSource(resource, version);
        if (fullPath == null)
        {
            if (pathConfigured)
            {
                // 配置了文件路径但磁盘文件缺失：标记失败，避免"处理完成却无生成物"的假成功
                await UpdateResourceMediaStatusAsync(resource.Id, ResourceMediaStatus.Failed);
                return MediaProcessOutcome.Fail("源文件不存在") with { ResourceVersionId = version?.Id };
            }

            // 无源文件（仅正文等），无需媒体处理
            await UpdateResourceMediaStatusAsync(resource.Id, ResourceMediaStatus.Ready);
            return MediaProcessOutcome.Ok with { ResourceVersionId = version?.Id };
        }

        var ext = Path.GetExtension(fullPath);
        var isImage = ImageExtensions.Contains(ext);
        var isVideo = VideoExtensions.Contains(ext);
        var isOffice = OfficeExtensions.Contains(ext);
        var isPdf = string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase);

        var failures = new List<string>();
        var planned = 0;

        if (isImage || isVideo)
        {
            planned++;
            await reportProgress(30, "正在生成缩略图…");
            await GenerateFfmpegThumbnailAsync(resource, version, versionKey, fullPath, failures, ct);
        }

        if (isPdf)
        {
            planned++;
            await reportProgress(40, "正在生成封面…");
            await GeneratePdfThumbnailAsync(resource, version, versionKey, fullPath, failures, ct);
        }

        if (isOffice)
        {
            // Office：预览 PDF + 由 PDF 首页生成封面
            planned += 2;
            await reportProgress(55, "正在转换预览…");
            var sizeBytes = new FileInfo(fullPath).Length;
            if (sizeBytes > _options.MaxPreviewFileSizeBytes)
            {
                failures.Add("预览 PDF");
                await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.PreviewPdf,
                    "full", "文件过大，暂不生成在线预览");
                failures.Add("封面");
                await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                    $"w{ThumbnailWidth}", "文件过大，未生成封面");
            }
            else
            {
                try
                {
                    var pdfAbs = await _officeConversionService.ConvertToPdfAsync(
                        resourceId.ToString(), fullPath, "preview", ct);
                    await UpsertArtifactAsync(resource, version, ResourceArtifactKind.PreviewPdf,
                        "full", pdfAbs, "application/pdf", ct);
                    await GeneratePdfThumbnailAsync(resource, version, versionKey, pdfAbs, failures, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MediaProcessor] 预览转换失败 resource={ResourceId}", resourceId);
                    failures.Add("预览 PDF");
                    await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.PreviewPdf,
                        "full", "文档转换失败");
                    failures.Add("封面");
                    await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                        $"w{ThumbnailWidth}", "文档转换失败");
                }
            }
        }

        await reportProgress(95, "收尾…");

        MediaProcessOutcome outcome;
        ResourceMediaStatus finalStatus;
        if (planned == 0 || failures.Count == 0)
        {
            finalStatus = ResourceMediaStatus.Ready;
            outcome = MediaProcessOutcome.Ok;
        }
        else if (failures.Count < planned)
        {
            finalStatus = ResourceMediaStatus.PartialFailed;
            outcome = MediaProcessOutcome.Partial($"部分生成物失败：{string.Join("、", failures)}");
        }
        else
        {
            finalStatus = ResourceMediaStatus.Failed;
            outcome = MediaProcessOutcome.Fail($"生成失败：{string.Join("、", failures)}");
        }

        await UpdateResourceMediaStatusAsync(resource.Id, finalStatus);
        return outcome with { ResourceVersionId = version?.Id };
    }

    /// <summary>
    /// 并发安全的资源 MediaStatus 回写。
    /// <para>
    /// 根因：<see cref="Resource"/> 是 <c>FullAuditedAggregateRoot</c>（带 ConcurrencyStamp）。
    /// <see cref="ProcessAsync"/> 开头 load 出实体后，要拿着它跑数分钟的转换
    /// （缩略图 / Gotenberg 转 PDF / PDF 光栅化），期间审批流（Status 变更）、
    /// <see cref="ResourceMediaJobManager.EnqueueAsync"/>（MediaStatus 置 Processing）、
    /// 用户编辑或另一个媒体任务都可能改了同一行并推高 ConcurrencyStamp。
    /// 最后直接回写这个旧实体，UPDATE 的 <c>WHERE Id AND ConcurrencyStamp=旧值</c>
    /// 命中 0 行，就会抛 <c>DbUpdateConcurrencyException</c>
    /// （expected 1 row(s) but actually affected 0 row(s)），任务被标失败，
    /// 而重试时 load 到的是新鲜戳所以能过——与生产报错现象一致。
    /// </para>
    /// <para>
    /// 修复：不再回写长持有的旧实体，每次都开独立 UoW 现查现改，
    /// 残余竞态窗口内冲突则重试（最多 3 次）。
    /// </para>
    /// </summary>
    private async Task UpdateResourceMediaStatusAsync(Guid resourceId, ResourceMediaStatus status)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
                var fresh = await _resourceRepository.FindAsync(resourceId);
                if (fresh == null)
                {
                    _logger.LogWarning("[MediaProcessor] 资源 {ResourceId} 已不存在，跳过 MediaStatus 回写", resourceId);
                    await uow.CompleteAsync();
                    return;
                }

                if (fresh.MediaStatus == status)
                {
                    await uow.CompleteAsync();
                    return;
                }

                fresh.MediaStatus = status;
                await _resourceRepository.UpdateAsync(fresh);
                await uow.CompleteAsync();
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "[MediaProcessor] 资源 {ResourceId} 回写 MediaStatus={Status} 遇到并发冲突，重试 {Attempt}/{Max}",
                    resourceId, status, attempt, maxAttempts);
            }
        }
    }

    private async Task GenerateFfmpegThumbnailAsync(
        Resource resource,
        ResourceVersion? version,
        string versionKey,
        string sourcePath,
        List<string> failures,
        CancellationToken ct)
    {
        try
        {
            var abs = await _thumbnailService.GetOrCreateAsync(versionKey, sourcePath, ThumbnailWidth, ct);
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
            _logger.LogWarning(ex, "[MediaProcessor] 缩略图失败 resource={ResourceId}", resource.Id);
            failures.Add("缩略图");
            await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                $"w{ThumbnailWidth}", "缩略图生成失败");
        }
    }

    private async Task GeneratePdfThumbnailAsync(
        Resource resource,
        ResourceVersion? version,
        string versionKey,
        string pdfFullPath,
        List<string> failures,
        CancellationToken ct)
    {
        try
        {
            var thumbDir = Path.Combine(_fileStorageService.RootPath, "thumbnails");
            var outPath = Path.Combine(thumbDir, $"{versionKey}_{ThumbnailWidth}.jpg");
            var abs = await _pdfPageRasterizer.RasterizeFirstPageAsync(pdfFullPath, outPath, ThumbnailWidth, ct);
            if (abs != null)
            {
                await UpsertArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                    $"w{ThumbnailWidth}", abs, "image/jpeg", ct);
            }
            else
            {
                failures.Add("封面");
                await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                    $"w{ThumbnailWidth}", "封面生成失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MediaProcessor] PDF 封面失败 resource={ResourceId}", resource.Id);
            failures.Add("封面");
            await UpsertFailedArtifactAsync(resource, version, ResourceArtifactKind.Thumbnail,
                $"w{ThumbnailWidth}", "封面生成失败");
        }
    }

    private async Task<ResourceVersion?> ResolveVersionAsync(Resource resource, Guid? versionId)
    {
        var resourceId = resource.Id;

        // 以资源实际对外提供的文件（Resource.FilePath）为准：下载/预览端点都取该路径，
        // 生成物必须与之一致。版本表的 IsCurrentVersion 可能因历史发布流程未同步而滞后，
        // 若仍按陈旧版本处理，会把错误的（甚至损坏的）文件交给转换/缩略图流水线。
        if (!string.IsNullOrWhiteSpace(resource.FilePath))
        {
            var byPath = await _versionRepository.FirstOrDefaultAsync(x =>
                x.ResourceId == resourceId && x.FilePath == resource.FilePath);
            if (byPath != null)
            {
                return byPath;
            }
        }

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

    private (string? fullPath, string versionKey, bool pathConfigured) ResolveSource(Resource resource, ResourceVersion? version)
    {
        var versionKey = (version?.Id ?? resource.Id).ToString();
        var filePath = version?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = resource.FilePath;
        }
        if (string.IsNullOrWhiteSpace(filePath))
        {
            // 未配置文件路径（仅正文等），无需媒体处理
            return (null, versionKey, false);
        }

        var fullPath = Path.Combine(_fileStorageService.RootPath, filePath);
        return File.Exists(fullPath) ? (fullPath, versionKey, true) : (null, versionKey, true);
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

        await UpsertArtifactCoreAsync(
            resource.Id, versionId, resource.TenantId, kind, variant,
            artifact => ApplyArtifact(artifact, relative, contentType, size),
            $"resource={resource.Id} version={versionId} kind={kind} variant={variant}");
    }

    /// <summary>
    /// Artifact 幂等 upsert：先查后插存在竞态（同资源双任务并发），本条立即提交，
    /// 用 (ResourceId, ResourceVersionId, Kind, Variant) 唯一索引做仲裁，
    /// 撞键（Postgres 23505）则回退为“重读后更新”，保证并发双跑收敛而非失败。
    /// </summary>
    private async Task UpsertArtifactCoreAsync(
        Guid resourceId,
        Guid versionId,
        Guid? tenantId,
        ResourceArtifactKind kind,
        string variant,
        Action<ResourceArtifact> mutate,
        string describe)
    {
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            var existing = await _artifactRepository.FirstOrDefaultAsync(x =>
                x.ResourceId == resourceId && x.ResourceVersionId == versionId && x.Kind == kind && x.Variant == variant);
            if (existing != null)
            {
                mutate(existing);
                await _artifactRepository.UpdateAsync(existing);
                await uow.CompleteAsync();
                return;
            }

            var artifact = new ResourceArtifact(_guidGenerator.Create(), resourceId, versionId, kind, variant)
            {
                TenantId = tenantId,
            };
            mutate(artifact);
            await _artifactRepository.InsertAsync(artifact);
            try
            {
                await uow.CompleteAsync();
                return;
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                _logger.LogWarning(ex, "[MediaProcessor] artifact 并发插入冲突，回退为更新 {Describe}", describe);
            }
        }

        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            var existing = await _artifactRepository.FirstOrDefaultAsync(x =>
                x.ResourceId == resourceId && x.ResourceVersionId == versionId && x.Kind == kind && x.Variant == variant);
            if (existing != null)
            {
                mutate(existing);
                await _artifactRepository.UpdateAsync(existing);
            }
            else
            {
                // 极端情况（胜者又删了行）：重新插入；再撞则抛给外层标失败走重试。
                var artifact = new ResourceArtifact(_guidGenerator.Create(), resourceId, versionId, kind, variant)
                {
                    TenantId = tenantId,
                };
                mutate(artifact);
                await _artifactRepository.InsertAsync(artifact);
            }
            await uow.CompleteAsync();
        }
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            // Application 层不直接引用 Npgsql，用反射读 SqlState（Postgres 唯一冲突为 23505）
            var sqlState = e.GetType().GetProperty("SqlState")?.GetValue(e) as string;
            if (sqlState == "23505")
            {
                return true;
            }
            if (e.Message.Contains("23505") ||
                e.Message.Contains("duplicate key value violates unique constraint"))
            {
                return true;
            }
        }
        return false;
    }

    private async Task UpsertFailedArtifactAsync(
        Resource resource,
        ResourceVersion? version,
        ResourceArtifactKind kind,
        string variant,
        string error)
    {
        var versionId = version?.Id ?? resource.Id;
        await UpsertArtifactCoreAsync(
            resource.Id, versionId, resource.TenantId, kind, variant,
            artifact =>
            {
                artifact.State = ResourceArtifactState.Failed;
                artifact.ErrorMessage = error;
                artifact.GeneratedAt = DateTime.UtcNow;
            },
            $"resource={resource.Id} version={versionId} kind={kind} variant={variant} (failed)");
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
