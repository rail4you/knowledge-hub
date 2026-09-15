using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Resources.Media;
using KnowledgeHub.Resources.Thumbnails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Controllers;

[Route("api/resource-file")]
public class ResourceFileController : AbpControllerBase
{
    protected IResourceRepository ResourceRepository { get; }
    protected IRepository<Resource, Guid> Repository { get; }
    protected IFileStorageService FileStorageService { get; }
    protected IDataFilter DataFilter { get; }
    protected IOptions<OfficeConversionOptions> ConversionOptions { get; }
    protected IResourceThumbnailService ThumbnailService { get; }
    protected IPdfPageRasterizer PdfPageRasterizer { get; }
    protected IRepository<ResourceArtifact, Guid> ArtifactRepository { get; }
    protected IMemoryCache MemoryCache { get; }

    public ResourceFileController(
        IResourceRepository resourceRepository,
        IRepository<Resource, Guid> repository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter,
        IOptions<OfficeConversionOptions> conversionOptions,
        IResourceThumbnailService thumbnailService,
        IPdfPageRasterizer pdfPageRasterizer,
        IRepository<ResourceArtifact, Guid> artifactRepository,
        IMemoryCache memoryCache)
    {
        ResourceRepository = resourceRepository;
        Repository = repository;
        FileStorageService = fileStorageService;
        DataFilter = dataFilter;
        ConversionOptions = conversionOptions;
        ThumbnailService = thumbnailService;
        PdfPageRasterizer = pdfPageRasterizer;
        ArtifactRepository = artifactRepository;
        MemoryCache = memoryCache;
    }

    /// <summary>状态/路径查询短缓存时长，降低预览轮询对 DB 的压力。</summary>
    private static readonly TimeSpan StatusCacheTtl = TimeSpan.FromSeconds(5);

    private sealed record PathCacheEntry(string? Path);

    [HttpGet("{resourceId}/download")]
    [Authorize(KnowledgeHubPermissions.Resources.Download)]
    public virtual async Task<IActionResult> Download(Guid resourceId)
    {
        // 与 Preview 一致：禁用多租户过滤器加载资源，
        // 否则跨租户/宿主上下文会抛 EntityNotFoundException（500）。
        // 注意：本 Controller 的 Action 返回 IActionResult（PhysicalFile/File），
        // ABP 的 AbpExceptionFilter 对这类 Action 不处理异常（见 ShouldHandleException：
        // 仅当返回 ObjectResult/DTO，或请求 Accept: application/json / Ajax 时才接管），
        // <video>/<img> 标签的预览请求两项都不满足，未捕获的 EntityNotFoundException
        // 会直接以原始 500 暴露出去，因此这里必须显式 catch 转为 404。
        Resource resource;
        try
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                resource = await ResourceRepository.GetWithDetailsAsync(resourceId);
            }
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { message = "资源文件不存在" });
        }

        // 仅允许下载审核通过的资源，或资源创建者本人（上传者随时可下载自己的待审核文件）。
        // 拒绝时返回 JSON 403，避免浏览器把 AccessDenied HTML 页面保存成下载文件（"4KB 错误文件"）。
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        var isCreator = CurrentUser.Id.HasValue && CurrentUser.Id.Value == resource.CreatorId;
        if (!isApproved && !isCreator)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "资源未审核通过，暂不可下载" });
        }

        // 资源设置了「不允许下载」时，一律禁止下载（仅支持在线预览）
        if (!resource.IsDownloadable)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "该资源不允许下载，仅支持在线预览" });
        }

        resource.DownloadCount++;
        await Repository.UpdateAsync(resource);

        var filePath = resource.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            var currentVersion = resource.Versions.FirstOrDefault(x => x.IsCurrentVersion);
            filePath = currentVersion?.FilePath;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            return NotFound(new { message = "资源文件不存在" });
        }

        try
        {
            // PhysicalFile 直接从磁盘流式输出（不把整个文件读入内存），
            // 并支持 Range（断点续传）。服务器内存有限，大文件（如 200MB PPTX）
            // 用 File(stream) 会整文件载入 MemoryStream，极易内存溢出。
            var fullPath = Path.Combine(FileStorageService.RootPath, filePath);
            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "资源文件不存在" });

            // 下载文件名必须带扩展名：OriginalFileName 缺失时回退到磁盘文件的真实名称
            // （老数据里有的 OriginalFileName 为空，直接用资源名"医疗"当下载文件名会
            // 导致保存下来的文件没有扩展名、双击打不开——"格式可能有问题"）。
            var fileName = resource.OriginalFileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileName(fullPath);
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = resource.Name ?? "download";
            }

            var contentType = GetContentType(fileName);
            return PhysicalFile(fullPath, contentType, fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Download] Failed to get file stream: {FilePath} for resource {ResourceId}", filePath, resourceId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "文件读取失败，请稍后重试" });
        }
    }

    [HttpGet("{resourceId}/preview")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> Preview(Guid resourceId, [FromQuery] bool countView = true)
    {
        Resource resource;
        try
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                resource = await ResourceRepository.GetWithDetailsAsync(resourceId);
            }
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { message = "资源文件不存在" });
        }

        // 审核通过的资源公开预览；待审核资源允许任意登录用户预览（教师/管理员可在审核前查看内容）。
        // 未登录用户预览待审核资源返回 403。
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        
        if (!isApproved)
        {
            if (!CurrentUser.IsAuthenticated)
            {
                return Forbid();
            }

            // 未审核资源仅限同租户查看（宿主管理员不受限）：
            // 租户用户只能看本租户的待审资源，宿主资源（TenantId 为空）对租户用户同样不可见，
            // 防止跨租户/跨层级预览他人待审文件。
            if (CurrentTenant.Id.HasValue && resource.TenantId != CurrentTenant.Id)
            {
                return Forbid();
            }
        }

        // 每次预览增加查看次数（封面缩略图用 countView=false，不计入，避免列表页刷出虚假浏览量）
        if (countView)
        {
            resource.ViewCount++;
            await Repository.UpdateAsync(resource);
        }
        else
        {
            // 封面/缩略图请求：允许浏览器私有缓存，避免列表页每次刷新都重新下载整份源文件。
            Response.Headers.CacheControl = "private, max-age=3600";
        }

        var filePath = resource.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            var currentVersion = resource.Versions.FirstOrDefault(x => x.IsCurrentVersion);
            filePath = currentVersion?.FilePath;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            return NotFound(new { message = "资源文件不存在" });
        }

        var fullPath = System.IO.Path.Combine(FileStorageService.RootPath, filePath);
        var fileName = resource.OriginalFileName ?? resource.Name ?? "preview";
        var contentType = GetContentType(fileName);

        // PhysicalFile throws FileNotFoundException if the file doesn't exist, resulting in 500.
        // Check existence first to return a proper 404 instead.
        if (!System.IO.File.Exists(fullPath))
        {
            Logger.LogWarning("[Preview] File not found: {FullPath} for resource {ResourceId}", fullPath, resourceId);
            return NotFound(new { message = "资源文件不存在，可能已被删除或路径变更" });
        }

        try
        {
            // PhysicalFile supports EnableRangeProcessing for chunked download
            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            // Exists 检查与实际打开之间的 TOCTOU 竞争（文件被删/权限变更/磁盘 IO 错误）
            // 同样会抛异常，这里转为 JSON 500，避免原始 500 暴露堆栈。
            Logger.LogError(ex, "[Preview] Failed to serve file: {FullPath} for resource {ResourceId}", fullPath, resourceId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "文件读取失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 资源封面缩略图：优先使用媒体流水线生成物；文档（PDF/Office）按请求宽度
    /// 从预览 PDF 首页光栅化（磁盘缓存）；图片/视频用 ffmpeg 缩放/抽帧。
    /// 传入较大的 w（如详情页 800）可得到高分屏清晰封面，不支持的类型返回 404。
    /// </summary>
    [HttpGet("{resourceId}/thumbnail")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> Thumbnail(Guid resourceId, [FromQuery] int w = 400)
    {
        // 1) 优先使用媒体处理流水线登记的当前版本生成物（跨租户历史数据也能命中）
        var artifact = await FindReadyThumbnailAsync(resourceId, w);
        if (artifact != null)
        {
            var artifactPath = Path.Combine(FileStorageService.RootPath, artifact.FilePath);
            if (System.IO.File.Exists(artifactPath))
            {
                Response.Headers.CacheControl = "private, max-age=86400";
                return PhysicalFile(artifactPath, artifact.ContentType ?? "image/jpeg");
            }
        }

        var fullPath = await GetResourceFullPathAsync(resourceId);

        // 2) 文档（PDF/Office）：按请求宽度光栅化预览 PDF 首页并磁盘缓存，
        //    使详情页等大尺寸展示也能拿到清晰封面（不再固定 400px）。
        var docThumb = fullPath != null
            ? await TryGetDocumentThumbnailAsync(resourceId, fullPath, w, HttpContext.RequestAborted)
            : null;
        if (docThumb != null)
        {
            Response.Headers.CacheControl = "private, max-age=86400";
            return PhysicalFile(docThumb, "image/jpeg");
        }

        // 3) 图片/视频：历史数据按需生成（不登记录入 artifact）
        if (fullPath == null)
            return NotFound(new { message = "资源文件不存在" });

        var thumbPath = await ThumbnailService.GetOrCreateAsync(
            resourceId.ToString(), fullPath, w, HttpContext.RequestAborted);
        if (thumbPath == null)
            return NotFound(new { message = "暂不支持缩略图" });

        Response.Headers.CacheControl = "private, max-age=86400";
        return PhysicalFile(thumbPath, "image/jpeg");
    }

    /// <summary>
    /// 文档封面：源文件是 PDF 时直接光栅化；Office 则复用媒体流水线生成的
    /// PreviewPdf 首页图。按 width 生成缓存文件（pdftoppm 命中已存在文件时直接复用）。
    /// </summary>
    private async Task<string?> TryGetDocumentThumbnailAsync(
        Guid resourceId, string sourceFullPath, int width, CancellationToken ct)
    {
        width = Math.Clamp(width, 64, 1600);

        string? pdfPath = null;
        if (string.Equals(Path.GetExtension(sourceFullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            pdfPath = sourceFullPath;
        }
        else
        {
            ResourceArtifact? preview = null;
            using (DataFilter.Disable<IMultiTenant>())
            {
                var list = await ArtifactRepository.GetListAsync(x =>
                    x.ResourceId == resourceId &&
                    x.Kind == ResourceArtifactKind.PreviewPdf &&
                    x.State == ResourceArtifactState.Ready);
                preview = list.OrderByDescending(x => x.GeneratedAt).FirstOrDefault();
            }
            if (preview != null)
            {
                var candidate = Path.Combine(FileStorageService.RootPath, preview.FilePath);
                if (System.IO.File.Exists(candidate))
                {
                    pdfPath = candidate;
                }
            }
        }

        if (pdfPath == null)
            return null;

        var outPath = Path.Combine(
            FileStorageService.RootPath, "thumbnails", $"{resourceId}_{width}.jpg");
        return await PdfPageRasterizer.RasterizeFirstPageAsync(pdfPath, outPath, width, ct);
    }

    private async Task<ResourceArtifact?> FindReadyThumbnailAsync(Guid resourceId, int width)
    {
        var variant = $"w{width}";
        using (DataFilter.Disable<IMultiTenant>())
        {
            var list = await ArtifactRepository.GetListAsync(x =>
                x.ResourceId == resourceId &&
                x.Kind == ResourceArtifactKind.Thumbnail &&
                x.Variant == variant &&
                x.State == ResourceArtifactState.Ready);
            return list.OrderByDescending(x => x.GeneratedAt).FirstOrDefault();
        }
    }

    /// <summary>
    /// 当前资源最新、状态 Ready 的预览 PDF 生成物（由媒体流水线在任务「预览完成」时登记）。
    /// </summary>
    private async Task<ResourceArtifact?> FindReadyPreviewPdfAsync(Guid resourceId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var list = await ArtifactRepository.GetListAsync(x =>
                x.ResourceId == resourceId &&
                x.Kind == ResourceArtifactKind.PreviewPdf &&
                x.State == ResourceArtifactState.Ready);
            return list.OrderByDescending(x => x.GeneratedAt).FirstOrDefault();
        }
    }

    /// <summary>
    /// Office 文档（PPT/PPTX/DOC/DOCX/XLS/XLSX）的 PDF 预览端点。
    /// 只返回媒体流水线已生成的 PreviewPdf 生成物（上传资源后自动转换）；
    /// 未转换完成时返回 409，前端通过 /preview-pdf-info 轮询等待，不在预览时触发转换。
    /// </summary>
    [HttpGet("{resourceId}/preview-pdf")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> PreviewPdf(Guid resourceId)
    {
        var artifact = await FindReadyPreviewPdfAsync(resourceId);
        if (artifact != null)
        {
            var artifactPath = Path.Combine(FileStorageService.RootPath, artifact.FilePath);
            if (System.IO.File.Exists(artifactPath))
            {
                // 转换结果按资源缓存，浏览器端同样允许私有缓存，重复预览无需再次下载整份 PDF。
                Response.Headers.CacheControl = "private, max-age=1800";
                // PhysicalFile 支持 Range 处理，pdfjs 流式加载需要
                return PhysicalFile(artifactPath, "application/pdf", enableRangeProcessing: true);
            }
        }

        // 生成物缺失：文档仍在转换中，或历史数据尚未由维护任务回填，前端保持 loading 等待即可。
        return StatusCode(StatusCodes.Status409Conflict, new { message = "文档尚未转换完成" });
    }

    /// <summary>
    /// 查询 Office 文档 PDF 预览的就绪状态（只读）。
    /// 媒体流水线生成 PreviewPdf 生成物（资源任务页「预览完成」）后 ready=true。
    /// 转换由上传触发的媒体任务自动完成；本端点只读状态、不触发转换，
    /// 前端轮询等待，未就绪时保持 loading 提示。
    /// </summary>
    [HttpGet("{resourceId}/preview-pdf-info")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> PreviewPdfInfo(Guid resourceId)
    {
        try
        {
            // 超大文件：媒体流水线不做转换，直接告知前端降级为下载查看。
            var fullPath = await GetResourceFullPathAsync(resourceId);
            if (fullPath != null &&
                new FileInfo(fullPath).Length > ConversionOptions.Value.MaxPreviewFileSizeBytes)
                return Ok(new { ready = false, count = 0, tooLarge = true });

            // 就绪判定：媒体流水线的 PreviewPdf 生成物存在且文件在盘。
            var artifact = await FindReadyPreviewPdfAsync(resourceId);
            if (artifact != null &&
                System.IO.File.Exists(Path.Combine(FileStorageService.RootPath, artifact.FilePath)))
                return Ok(new { ready = true, count = 0 });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[PreviewPdfInfo] 查询转换状态失败: {ResourceId}", resourceId);
        }

        return Ok(new { ready = false, count = 0 });
    }


    private async Task<string?> GetResourceFullPathAsync(Guid resourceId)
    {
        // 预览轮询会高频调用，加 5s 短缓存，避免每次 GetWithDetailsAsync（Include 版本/审核）
        var cacheKey = $"resfile:path:{resourceId}:{CurrentTenant.Id}:{CurrentUser.IsAuthenticated}";
        if (MemoryCache.TryGetValue(cacheKey, out PathCacheEntry? cached) && cached != null)
        {
            return cached.Path;
        }

        var resolved = await ResolveResourceFullPathAsync(resourceId);
        MemoryCache.Set(cacheKey, new PathCacheEntry(resolved), StatusCacheTtl);
        return resolved;
    }

    private async Task<string?> ResolveResourceFullPathAsync(Guid resourceId)
    {
        Resource resource;
        try
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                resource = await ResourceRepository.GetWithDetailsAsync(resourceId);
            }
        }
        catch (EntityNotFoundException)
        {
            // 调用方（Preview / PreviewPdf / PreviewPdfInfo）据此返回 404。
            // 必须在这里 catch：本 Controller 的 IActionResult Action 抛出的异常
            // 不会被 AbpExceptionFilter 接管（见 Download 处的注释），否则直接 500。
            return null;
        }

        // 与 Preview 方法保持一致的权限检查：
        // 审核通过的资源公开预览；待审核资源仅同租户登录用户可预览（教师/管理员审核前查看）。
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        if (!isApproved)
        {
            if (!CurrentUser.IsAuthenticated)
                return null;

            // 未审核资源：租户用户仅可访问本租户（宿主资源对租户用户不可见）
            if (CurrentTenant.Id.HasValue && resource.TenantId != CurrentTenant.Id)
                return null;
        }

        var filePath = resource.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            var currentVersion = resource.Versions.FirstOrDefault(x => x.IsCurrentVersion);
            filePath = currentVersion?.FilePath;
        }

        if (string.IsNullOrEmpty(filePath))
            return null;

        var fullPath = Path.Combine(FileStorageService.RootPath, filePath);
        if (!System.IO.File.Exists(fullPath))
            return null;

        return fullPath;
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}
