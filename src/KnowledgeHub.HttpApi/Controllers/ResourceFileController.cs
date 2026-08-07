using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Controllers;

public class SlideTextDto
{
    public string Text { get; set; } = string.Empty;
    public int FontSize { get; set; } = 18;
    public bool Bold { get; set; }
    public string Color { get; set; } = "#333333";
}

/// <summary>幻灯片中的单个形状（文本框 / 图片），坐标为 EMU（914400 EMU = 1 英寸）。</summary>
public class SlideShapeDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public string? Align { get; set; }
    public string? Anchor { get; set; }
    public List<SlideTextDto> Texts { get; set; } = new();
    public string? Image { get; set; }
}

/// <summary>整张幻灯片：尺寸 + 形状列表，前端按坐标渲染，无需 soffice。</summary>
public class SlideDataDto
{
    public int SlideNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public List<SlideShapeDto> Shapes { get; set; } = new();
}

[Route("api/resource-file")]
public class ResourceFileController : AbpControllerBase
{
    protected IResourceRepository ResourceRepository { get; }
    protected IRepository<Resource, Guid> Repository { get; }
    protected IFileStorageService FileStorageService { get; }
    protected IDataFilter DataFilter { get; }
    protected IOfficeConversionService OfficeConversionService { get; }

    public ResourceFileController(
        IResourceRepository resourceRepository,
        IRepository<Resource, Guid> repository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter,
        IOfficeConversionService officeConversionService)
    {
        ResourceRepository = resourceRepository;
        Repository = repository;
        FileStorageService = fileStorageService;
        DataFilter = dataFilter;
        OfficeConversionService = officeConversionService;
    }

    /// <summary>
    /// 截断/损坏的 PPTX（缺 ZIP 中央目录）无法用 ZipFile 定位条目，只能扫描本地文件头。
    /// 每次扫描都要读完整源文件（可能几十 MB）。这里按 (Length, LastWriteTimeUtc)
    /// 缓存扫描结果，供 slides/media 复用，避免每个幻灯片请求都重新读一遍源文件。
    /// </summary>
    private static readonly ConcurrentDictionary<string, ZipScanCacheEntry> ZipScanCache = new();

    private sealed class ZipScanCacheEntry
    {
        public required long Length { get; init; }
        public required DateTime LastWriteTimeUtc { get; init; }
        public required IReadOnlyDictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)> Entries { get; init; }
    }

    private static IReadOnlyDictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)> GetCachedZipEntries(string fullPath)
    {
        var info = new FileInfo(fullPath);
        if (ZipScanCache.TryGetValue(fullPath, out var cached) &&
            cached.Length == info.Length &&
            cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
        {
            return cached.Entries;
        }

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var entries = ScanZipLocalHeaders(fs);
        ZipScanCache[fullPath] = new ZipScanCacheEntry
        {
            Length = info.Length,
            LastWriteTimeUtc = info.LastWriteTimeUtc,
            Entries = entries,
        };

        // 简单防膨胀：超过阈值整体清空（单条目仅几十 KB）
        if (ZipScanCache.Count > 500)
        {
            ZipScanCache.Clear();
        }

        return entries;
    }

    [HttpGet("{resourceId}/download")]
    [Authorize(KnowledgeHubPermissions.Resources.Download)]
    public virtual async Task<IActionResult> Download(Guid resourceId)
    {
        // 与 Preview 一致：禁用多租户过滤器加载资源，
        // 否则跨租户/宿主上下文会抛 EntityNotFoundException（500）。
        Resource resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await ResourceRepository.GetWithDetailsAsync(resourceId);
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

            var fileName = resource.OriginalFileName ?? resource.Name ?? "download";
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
    public virtual async Task<IActionResult> Preview(Guid resourceId)
    {
        Resource resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await ResourceRepository.GetWithDetailsAsync(resourceId);
        }

        // 审核通过的资源公开预览；待审核资源允许任意登录用户预览（教师/管理员可在审核前查看内容）。
        // 未登录用户预览待审核资源返回 403。
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        
        if (!isApproved && !CurrentUser.IsAuthenticated)
        {
            return Forbid();
        }

        // 每次预览增加查看次数
        resource.ViewCount++;
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

        // PhysicalFile supports EnableRangeProcessing for chunked download
        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Office 文档（PPTX/DOCX/XLSX）的 PDF 预览端点。
    /// 后端通过 LibreOffice headless 转换为 PDF，缓存到 converted/{id}.pdf。
    /// 前端用 pdfjs-dist 渲染返回的 PDF。
    /// 首次转换可能耗时 5-60s（80MB PPTX 实测 ~9s），后续缓存命中毫秒级返回。
    /// </summary>
    [HttpGet("{resourceId}/preview-pdf")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> PreviewPdf(Guid resourceId)
    {
        var fullPath = await GetResourceFullPathAsync(resourceId);
        if (fullPath == null)
            return NotFound(new { message = "资源文件不存在" });

        var ext = Path.GetExtension(fullPath)?.ToLowerInvariant();
        if (ext != ".pptx" && ext != ".docx" && ext != ".xlsx" && ext != ".ppt" && ext != ".doc" && ext != ".xls")
            return BadRequest(new { message = "仅支持 Office 文档（PPTX/DOCX/XLSX）" });

        try
        {
            // 截断/损坏的 PPTX 在 LibreOfficeConversionService 内部先重建 ZIP 中央目录修复，
            // 再走 soffice 转 PDF（保持原始版式）。无法修复时抛 OfficeConversionException。
            var pdfPath = await OfficeConversionService.ConvertToPdfAsync(
                resourceId.ToString(), fullPath);

            // PhysicalFile 支持 Range 处理，pdfjs 需要
            return PhysicalFile(pdfPath, "application/pdf", enableRangeProcessing: true);
        }
        catch (OfficeConversionException ex)
        {
            Logger.LogWarning(ex, "[PreviewPdf] 转换失败: {ResourceId}", resourceId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "文档转换失败，请下载后查看",
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[PreviewPdf] 未知错误: {ResourceId}", resourceId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "预览服务异常，请稍后重试",
            });
        }
    }

    /// <summary>
    /// 获取单页 PDF（PdfSharp 拆分后的缓存页面，每页仅 ~200KB）。
    /// 前端首页秒出，后续按需加载。
    /// </summary>
    [HttpGet("{resourceId}/preview-pdf-page/{pageNumber:int}")]
    [AllowAnonymous]
    public virtual IActionResult PreviewPdfPage(Guid resourceId, int pageNumber)
    {
        var pagePath = OfficeConversionService.GetPagePdfPath(
            resourceId.ToString(), pageNumber);

        if (!System.IO.File.Exists(pagePath))
            return NotFound(new { message = $"页面 {pageNumber} 不存在或尚未转换" });

        return PhysicalFile(pagePath, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// 查询 PDF 预览的就绪状态与总页数。
    /// 拆分完成时由 SplitPdfToPages 写入 {resourceId}/.count 边车文件。
    /// 前端轮询此端点直到 ready=true，避免对每页做 HEAD 探测。
    /// </summary>
    [HttpGet("{resourceId}/preview-pdf-info")]
    [AllowAnonymous]
    public virtual IActionResult PreviewPdfInfo(Guid resourceId)
    {
        var pagesDir = Path.GetDirectoryName(
            OfficeConversionService.GetPagePdfPath(resourceId.ToString(), 1))!;
        var countFile = Path.Combine(pagesDir, ".count");

        if (!System.IO.File.Exists(countFile))
            return Ok(new { ready = false, count = 0 });

        var raw = System.IO.File.ReadAllText(countFile).Trim();
        if (!int.TryParse(raw, out var count) || count <= 0)
            return Ok(new { ready = false, count = 0 });

        return Ok(new { ready = true, count });
    }

    /// <summary>
    /// 获取 PPTX 幻灯片总数（按需加载，不下载整个文件）
    /// </summary>
    [HttpGet("{resourceId}/slides/count")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> GetSlideCount(Guid resourceId)
    {
        var fullPath = await GetResourceFullPathAsync(resourceId);
        if (fullPath == null)
            return NotFound(new { message = "资源文件不存在" });

        var ext = Path.GetExtension(fullPath)?.ToLowerInvariant();
        if (ext != ".pptx")
            return BadRequest(new { message = "仅支持 PPTX 文件" });

        try
        {
            // 优先使用 ZipFile.OpenRead（需要完整 ZIP 中央目录）
            // 降级：直接扫描本地文件头（兼容截断的 PPTX 文件）
            var count = TryGetSlideCountViaZip(fullPath) ?? ScanLocalFileHeaders(fullPath);
            return new JsonResult(new { count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"无法读取 PPTX 文件: {ex.Message}" });
        }
    }

    private static int? TryGetSlideCountViaZip(string fullPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(fullPath);
            return archive.Entries.Count(e =>
                Regex.IsMatch(e.FullName, @"^ppt/slides/slide\d+\.xml$"));
        }
        catch
        {
            return null;
        }
    }

    private static int ScanLocalFileHeaders(string fullPath)
    {
        // 复用缓存的本地头扫描结果（首次扫描后不再重读整个文件）
        var entries = GetCachedZipEntries(fullPath);
        var slidePattern = new Regex(@"^ppt/slides/slide\d+\.xml$", RegexOptions.Compiled);
        return entries.Keys.Count(name => slidePattern.IsMatch(name));
    }

    /// <summary>
    /// 获取 PPTX 单张幻灯片的内容（文本 + 图片引用，按需提取）
    /// </summary>
    [HttpGet("{resourceId}/slides/{slideNumber:int}")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> GetSlide(Guid resourceId, int slideNumber)
    {
        var fullPath = await GetResourceFullPathAsync(resourceId);
        if (fullPath == null)
            return NotFound(new { message = "资源文件不存在" });

        var ext = Path.GetExtension(fullPath)?.ToLowerInvariant();
        if (ext != ".pptx")
            return BadRequest(new { message = "仅支持 PPTX 文件" });

        try
        {
            // 优先使用 ZipFile.OpenRead（标准 ZIP），降级到扫描本地文件头
            if (TryReadSlideViaZip(fullPath, slideNumber, out var result))
                return new JsonResult(result);

            result = ReadSlideViaLocalHeaders(fullPath, slideNumber);
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"幻灯片提取失败: {ex.Message}" });
        }
    }

    private bool TryReadSlideViaZip(string fullPath, int slideNumber, out object result)
    {
        result = null!;
        try
        {
            using var archive = ZipFile.OpenRead(fullPath);
            var slideEntry = archive.GetEntry($"ppt/slides/slide{slideNumber}.xml");
            if (slideEntry == null)
                return false;

            XDocument? relsDoc = null;
            var relsEntry = archive.GetEntry($"ppt/slides/_rels/slide{slideNumber}.xml.rels");
            if (relsEntry != null)
            {
                using var relsStream = relsEntry.Open();
                relsDoc = XDocument.Load(relsStream);
            }

            var (width, height) = ReadSlideSize(archive);

            using var stream = slideEntry.Open();
            var doc = XDocument.Load(stream);
            result = ParseSlide(doc, relsDoc, slideNumber, width, height, fullPath => archive.GetEntry(fullPath) != null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private object ReadSlideViaLocalHeaders(string fullPath, int slideNumber)
    {
        var slideName = $"ppt/slides/slide{slideNumber}.xml";
        var relsName = $"ppt/slides/_rels/slide{slideNumber}.xml.rels";

        // 复用缓存的本地头扫描结果，避免每个幻灯片都重读整个源文件
        var entries = GetCachedZipEntries(fullPath);

        if (!entries.TryGetValue(slideName, out var slideEntryInfo))
            throw new InvalidOperationException($"幻灯片 {slideNumber} 不存在");

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] slideData = ExtractEntryData(fs, slideEntryInfo);
        using var slideMs = new MemoryStream(slideData);
        var doc = XDocument.Load(slideMs);

        XDocument? relsDoc = null;
        if (entries.TryGetValue(relsName, out var relsEntryInfo))
        {
            var relsData = ExtractEntryData(fs, relsEntryInfo);
            using var relsMs = new MemoryStream(relsData);
            relsDoc = XDocument.Load(relsMs);
        }

        var (width, height) = ReadSlideSize(entries, fs);

        return ParseSlide(doc, relsDoc, slideNumber, width, height, name => entries.ContainsKey(name));
    }

    private static byte[] ExtractEntryData(FileStream fs, (long offset, int headerSize, int compressSize, ushort method, ushort flags) entry)
    {
        long dataOffset = entry.offset + entry.headerSize;
        int dataSize = entry.compressSize;

        if (entry.method == 0)
        {
            // Stored - read directly
            byte[] data = new byte[dataSize];
            fs.Position = dataOffset;
            fs.ReadExactly(data, 0, dataSize);
            return data;
        }
        else if (entry.method == 8)
        {
            // Deflated - decompress
            byte[] compressed = new byte[dataSize];
            fs.Position = dataOffset;
            fs.ReadExactly(compressed, 0, dataSize);
            using var compressedMs = new MemoryStream(compressed);
            using var deflate = new DeflateStream(compressedMs, CompressionMode.Decompress);
            using var resultMs = new MemoryStream(dataSize * 2);
            deflate.CopyTo(resultMs);
            return resultMs.ToArray();
        }
        else
        {
            throw new NotSupportedException($"不支持的压缩方法: {entry.method}");
        }
    }

    private static int FindNextPkSignature(byte[] buffer, int length, int start)
    {
        for (int i = start; i < length - 4; i++)
        {
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B &&
                buffer[i + 2] == 0x03 && buffer[i + 3] == 0x04)
                return i;
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B &&
                buffer[i + 2] == 0x01 && buffer[i + 3] == 0x02)
                return i; // Also stop at central directory
        }
        return -1;
    }

    /// <summary>
    /// 从 slide XML 中提取带位置的形状（文本框 / 图片），供前端按坐标还原版式。
    /// 不依赖 LibreOffice/soffice。
    /// </summary>
    private static SlideDataDto ParseSlide(
        XDocument doc,
        XDocument? relsDoc,
        int slideNumber,
        double slideWidth,
        double slideHeight,
        Func<string, bool> entryExists)
    {
        var aNs = XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main");
        var rNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        var pNs = XNamespace.Get("http://schemas.openxmlformats.org/presentationml/2006/main");

        // rels: rId -> 解析后的媒体路径
        var relMap = new Dictionary<string, string>();
        if (relsDoc != null)
        {
            var relNs = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
            foreach (var rel in relsDoc.Descendants(relNs + "Relationship"))
            {
                var id = rel.Attribute("Id")?.Value;
                var target = rel.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                    relMap[id] = ResolveRelativePath("ppt/slides", target);
            }
        }

        var shapes = new List<SlideShapeDto>();

        // 文本框 / 形状：p:sp
        foreach (var sp in doc.Descendants(pNs + "sp"))
        {
            var shape = new SlideShapeDto();
            ParseXfrm(sp.Element(pNs + "xfrm"), aNs, shape);
            shape.Texts = ExtractShapeTexts(sp, aNs);

            var pPr = sp.Descendants(aNs + "pPr").FirstOrDefault();
            shape.Align = pPr?.Attribute("algn")?.Value;
            var bodyPr = sp.Element(pNs + "txBody")?.Element(aNs + "bodyPr");
            shape.Anchor = bodyPr?.Attribute("anchor")?.Value;

            if (shape.W <= 0 || shape.H <= 0)
            {
                // 无尺寸的形状：有文字则给一行默认高度，纯装饰则跳过
                if (shape.Texts.Count == 0) continue;
                shape.W = slideWidth;
                shape.H = 60 * 12700;
            }
            shapes.Add(shape);
        }

        // 图片：p:pic
        foreach (var pic in doc.Descendants(pNs + "pic"))
        {
            var shape = new SlideShapeDto();
            ParseXfrm(pic.Element(pNs + "xfrm"), aNs, shape);

            var blip = pic.Descendants(aNs + "blip").FirstOrDefault();
            var embed = blip?.Attribute(rNs + "embed")?.Value;
            if (embed != null && relMap.TryGetValue(embed, out var target) && entryExists(target))
                shape.Image = target;

            if (shape.Image != null && (shape.W > 0 || shape.H > 0))
                shapes.Add(shape);
        }

        return new SlideDataDto
        {
            SlideNumber = slideNumber,
            Width = slideWidth,
            Height = slideHeight,
            Shapes = shapes,
        };
    }

    private static void ParseXfrm(XElement? xfrm, XNamespace aNs, SlideShapeDto shape)
    {
        if (xfrm == null) return;
        var off = xfrm.Element(aNs + "off");
        var ext = xfrm.Element(aNs + "ext");
        double.TryParse(off?.Attribute("x")?.Value, out var x);
        double.TryParse(off?.Attribute("y")?.Value, out var y);
        double.TryParse(ext?.Attribute("cx")?.Value, out var w);
        double.TryParse(ext?.Attribute("cy")?.Value, out var h);
        shape.X = x;
        shape.Y = y;
        shape.W = w;
        shape.H = h;
    }

    private static List<SlideTextDto> ExtractShapeTexts(XElement shapeEl, XNamespace aNs)
    {
        var texts = new List<SlideTextDto>();
        foreach (var tEl in shapeEl.Descendants(aNs + "t"))
        {
            var text = tEl.Value;
            if (string.IsNullOrWhiteSpace(text)) continue;

            int fontSize = 18;
            bool bold = false;
            string color = "#333333";

            var rPr = tEl.Parent?.Element(aNs + "rPr");
            if (rPr != null)
            {
                var sz = rPr.Attribute("sz");
                if (sz != null && int.TryParse(sz.Value, out var szVal))
                    fontSize = szVal / 100;
                bold = rPr.Element(aNs + "b") != null;
                var solidFill = rPr.Element(aNs + "solidFill");
                if (solidFill != null)
                {
                    var srgb = solidFill.Element(aNs + "srgbClr");
                    if (srgb != null)
                    {
                        var val = srgb.Attribute("val");
                        if (val != null) color = "#" + val.Value;
                    }
                }
            }

            texts.Add(new SlideTextDto { Text = text, FontSize = fontSize, Bold = bold, Color = color });
        }
        return texts;
    }

    /// <summary>读取幻灯片尺寸（EMU），缺失时默认 16:9。</summary>
    private static (double width, double height) ParseSlideSize(XDocument? presDoc)
    {
        const double defaultW = 12192000;
        const double defaultH = 6858000;
        if (presDoc == null) return (defaultW, defaultH);

        var pNs = XNamespace.Get("http://schemas.openxmlformats.org/presentationml/2006/main");
        var sldSz = presDoc.Descendants(pNs + "sldSz").FirstOrDefault();
        if (sldSz == null) return (defaultW, defaultH);

        if (!double.TryParse(sldSz.Attribute("cx")?.Value, out var w) || w <= 0) w = defaultW;
        if (!double.TryParse(sldSz.Attribute("cy")?.Value, out var h) || h <= 0) h = defaultH;
        return (w, h);
    }

    private static (double width, double height) ReadSlideSize(ZipArchive archive)
    {
        var entry = archive.GetEntry("ppt/presentation.xml");
        if (entry == null) return (12192000, 6858000);
        using var stream = entry.Open();
        return ParseSlideSize(XDocument.Load(stream));
    }

    private static (double width, double height) ReadSlideSize(
        IReadOnlyDictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)> entries,
        FileStream fs)
    {
        if (entries.TryGetValue("ppt/presentation.xml", out var info))
        {
            var data = ExtractEntryData(fs, info);
            using var ms = new MemoryStream(data);
            return ParseSlideSize(XDocument.Load(ms));
        }
        return (12192000, 6858000);
    }

    /// <summary>
    /// 获取 PPTX 内嵌媒体文件（图片等），路径格式: ppt/media/image1.png
    /// </summary>
    [HttpGet("{resourceId}/media/{*mediaPath}")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> GetMedia(Guid resourceId, string mediaPath)
    {
        var fullPath = await GetResourceFullPathAsync(resourceId);
        if (fullPath == null)
            return NotFound(new { message = "资源文件不存在" });

        var ext = Path.GetExtension(fullPath)?.ToLowerInvariant();
        if (ext != ".pptx")
            return BadRequest(new { message = "仅支持 PPTX 文件" });

        try
        {
            var data = TryReadMediaViaZip(fullPath, mediaPath)
                       ?? ReadMediaViaLocalHeaders(fullPath, mediaPath);
            if (data == null)
                return NotFound(new { message = "媒体文件不存在" });

            var contentType = GetContentType(mediaPath);
            return File(data, contentType);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"媒体提取失败: {ex.Message}" });
        }
    }

    private byte[]? TryReadMediaViaZip(string fullPath, string mediaPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(fullPath);
            var entry = archive.GetEntry(mediaPath);
            if (entry == null) return null;

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private byte[]? ReadMediaViaLocalHeaders(string fullPath, string mediaPath)
    {
        try
        {
            // 复用缓存的本地头扫描结果，避免每次取图都重读整个源文件
            var entries = GetCachedZipEntries(fullPath);
            if (!entries.TryGetValue(mediaPath, out var entryInfo))
                return null;

            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ExtractEntryData(fs, entryInfo);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)> ScanZipLocalHeaders(FileStream fs)
    {
        var entries = new Dictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)>();
        var buf = new byte[30];
        long fileLen = fs.Length;
        long pos = 0;

        while (pos < fileLen - 4)
        {
            fs.Position = pos;
            int read = fs.Read(buf, 0, 4);
            if (read < 4) break;

            if (buf[0] == 0x50 && buf[1] == 0x4B && buf[2] == 0x03 && buf[3] == 0x04)
            {
                fs.Position = pos;
                read = fs.Read(buf, 0, 30);
                if (read < 30) break;

                ushort method = (ushort)(buf[8] | (buf[9] << 8));
                ushort flags = (ushort)(buf[6] | (buf[7] << 8));
                int compressSize = buf[18] | (buf[19] << 8) | (buf[20] << 16) | (buf[21] << 24);
                int fileNameLen = buf[26] | (buf[27] << 8);
                int extraLen = buf[28] | (buf[29] << 8);
                int headerSize = 30 + fileNameLen + extraLen;

                byte[] fnBytes = new byte[fileNameLen];
                fs.Position = pos + 30;
                fs.ReadExactly(fnBytes, 0, fileNameLen);
                string fileName = Encoding.UTF8.GetString(fnBytes);

                bool hasDataDescriptor = (flags & 8) != 0;
                if (hasDataDescriptor)
                {
                    long dataStart = pos + headerSize;
                    long scanPos = dataStart;
                    int searchBufSize = (int)Math.Min(1024 * 1024, fileLen - scanPos);
                    byte[] searchBuf = new byte[searchBufSize];
                    fs.Position = scanPos;
                    int searchRead = fs.Read(searchBuf, 0, searchBufSize);
                    int pkPos = FindNextPkSignature(searchBuf, searchRead, 0);
                    compressSize = pkPos >= 0 ? pkPos : searchRead;
                }

                entries[fileName] = (pos, headerSize, compressSize, method, flags);
                pos += headerSize + compressSize;
            }
            else
            {
                pos++;
            }
        }

        return entries;
    }

    private static string ResolveRelativePath(string baseDir, string target)
    {
        var parts = target.Split('/');
        var baseParts = baseDir.Split('/').ToList();
        foreach (var part in parts)
        {
            if (part == "..") baseParts.RemoveAt(baseParts.Count - 1);
            else baseParts.Add(part);
        }
        return string.Join("/", baseParts);
    }

    private async Task<string?> GetResourceFullPathAsync(Guid resourceId)
    {
        Resource resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await ResourceRepository.GetWithDetailsAsync(resourceId);
        }

        // 与 Preview 方法保持一致的权限检查：
        // 审核通过的资源公开预览；待审核资源仅登录用户可预览（教师/管理员审核前查看）。
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        if (!isApproved && !CurrentUser.IsAuthenticated)
            return null;

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
