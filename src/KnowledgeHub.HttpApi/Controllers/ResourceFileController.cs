using System;
using System.Buffers;
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
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

[Route("api/resource-file")]
public class ResourceFileController : AbpControllerBase
{
    protected IResourceRepository ResourceRepository { get; }
    protected IRepository<Resource, Guid> Repository { get; }
    protected IFileStorageService FileStorageService { get; }
    protected IDataFilter DataFilter { get; }

    public ResourceFileController(
        IResourceRepository resourceRepository,
        IRepository<Resource, Guid> repository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter)
    {
        ResourceRepository = resourceRepository;
        Repository = repository;
        FileStorageService = fileStorageService;
        DataFilter = dataFilter;
    }

    [HttpGet("{resourceId}/download")]
    [Authorize(KnowledgeHubPermissions.Resources.Download)]
    public virtual async Task<IActionResult> Download(Guid resourceId)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(resourceId);

        // Only allow downloading approved resources
        if (resource.Status != ResourceStatus.SchoolApproved &&
            resource.Status != ResourceStatus.LeagueApproved)
        {
            return Forbid();
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

        var stream = await FileStorageService.GetAsync(filePath);
        var fileName = resource.OriginalFileName ?? resource.Name ?? "download";

        var contentType = GetContentType(fileName);

        return File(stream, contentType, fileName);
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

        // Allow preview if resource is approved, OR if current user is the creator
        // (so uploaders can preview their own content before submitting for review)
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        var isCreator = CurrentUser.Id.HasValue && CurrentUser.Id.Value == resource.CreatorId;
        
        if (!isApproved && !isCreator)
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

        // PhysicalFile supports EnableRangeProcessing for chunked download
        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
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
        var count = 0;
        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            var slidePattern = new Regex(@"^ppt/slides/slide\d+\.xml$", RegexOptions.Compiled);
            long fileLen = fs.Length;
            long pos = 0;

            while (pos < fileLen - 4)
            {
                // Read chunk and scan for PK\x03\x04 signatures
                fs.Position = pos;
                int read = fs.Read(buffer, 0, buffer.Length);
                if (read < 4) break;

                int localPos = 0;
                while (localPos < read - 4)
                {
                    if (buffer[localPos] == 0x50 && buffer[localPos + 1] == 0x4B &&
                        buffer[localPos + 2] == 0x03 && buffer[localPos + 3] == 0x04)
                    {
                        // Parse local file header
                        int remaining = read - localPos;
                        if (remaining < 30) break;

                        int fileNameLen = buffer[localPos + 26] | (buffer[localPos + 27] << 8);
                        int extraLen = buffer[localPos + 28] | (buffer[localPos + 29] << 8);
                        int headerSize = 30 + fileNameLen + extraLen;

                        if (localPos + headerSize > read) break;

                        string fileName = Encoding.UTF8.GetString(
                            buffer, localPos + 30, fileNameLen);

                        if (slidePattern.IsMatch(fileName))
                            count++;

                        // Get compressed size
                        int compressSize = buffer[localPos + 18] | (buffer[localPos + 19] << 8) |
                                           (buffer[localPos + 20] << 16) | (buffer[localPos + 21] << 24);

                        localPos += headerSize + compressSize;
                    }
                    else
                    {
                        localPos++;
                    }
                }

                pos += localPos;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return count;
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

            using var stream = slideEntry.Open();
            result = ParseSlideXml(stream, archive, slideNumber, fullPath => archive.GetEntry(fullPath) != null);
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

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var entries = ScanZipLocalHeaders(fs);

        if (!entries.TryGetValue(slideName, out var slideEntryInfo))
            throw new InvalidOperationException($"幻灯片 {slideNumber} 不存在");

        byte[] slideData = ExtractEntryData(fs, slideEntryInfo);
        using var slideMs = new MemoryStream(slideData);
        var doc = XDocument.Load(slideMs);

        byte[]? relsData = null;
        if (entries.TryGetValue(relsName, out var relsEntryInfo))
        {
            relsData = ExtractEntryData(fs, relsEntryInfo);
        }

        var aNs = XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main");
        var rNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var texts = ExtractTexts(doc, aNs);
        var images = ExtractImages(doc, aNs, rNs, relsData, slideNumber, entries);

        return new { slideNumber, texts, images };
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

    private static List<SlideTextDto> ExtractTexts(XDocument doc, XNamespace aNs)
    {
        var texts = new List<SlideTextDto>();
        foreach (var tEl in doc.Descendants(aNs + "t"))
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

    private static List<string> ExtractImages(XDocument doc, XNamespace aNs, XNamespace rNs, byte[]? relsData, int slideNumber, IReadOnlyDictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)> entries)
    {
        var embedIds = new HashSet<string>();
        foreach (var blip in doc.Descendants(aNs + "blip"))
        {
            var embed = blip.Attribute(rNs + "embed");
            if (embed != null && !string.IsNullOrEmpty(embed.Value))
                embedIds.Add(embed.Value);
        }

        var images = new List<string>();
        if (embedIds.Count > 0 && relsData != null)
        {
            using var relsMs = new MemoryStream(relsData);
            var relsDoc = XDocument.Load(relsMs);
            var relNs = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
            foreach (var rel in relsDoc.Descendants(relNs + "Relationship"))
            {
                var id = rel.Attribute("Id")?.Value;
                var target = rel.Attribute("Target")?.Value;
                if (id != null && target != null && embedIds.Contains(id))
                {
                    var resolved = ResolveRelativePath("ppt/slides", target);
                    if (entries.ContainsKey(resolved))
                        images.Add(resolved);
                }
            }
        }
        return images;
    }

    private object ParseSlideXml(Stream stream, ZipArchive archive, int slideNumber, Func<string, bool> entryExists)
    {
        var doc = XDocument.Load(stream);
        var aNs = XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main");
        var rNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var texts = ExtractTexts(doc, aNs);

        // For zip-archive mode, read rels separately
        var embedIds = new HashSet<string>();
        foreach (var blip in doc.Descendants(aNs + "blip"))
        {
            var embed = blip.Attribute(rNs + "embed");
            if (embed != null && !string.IsNullOrEmpty(embed.Value))
                embedIds.Add(embed.Value);
        }

        var images = new List<string>();
        if (embedIds.Count > 0)
        {
            var relsEntry = archive.GetEntry($"ppt/slides/_rels/slide{slideNumber}.xml.rels");
            if (relsEntry != null)
            {
                using var relsStream = relsEntry.Open();
                var relsDoc = XDocument.Load(relsStream);
                var relNs = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
                foreach (var rel in relsDoc.Descendants(relNs + "Relationship"))
                {
                    var id = rel.Attribute("Id")?.Value;
                    var target = rel.Attribute("Target")?.Value;
                    if (id != null && target != null && embedIds.Contains(id))
                    {
                        var resolved = ResolveRelativePath("ppt/slides", target);
                        if (entryExists(resolved))
                            images.Add(resolved);
                    }
                }
            }
        }

        return new { slideNumber, texts, images };
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
            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entries = ScanZipLocalHeaders(fs);
            if (!entries.TryGetValue(mediaPath, out var entryInfo))
                return null;

            return ExtractEntryData(fs, entryInfo);
        }
        catch
        {
            return null;
        }
    }

    private Dictionary<string, (long offset, int headerSize, int compressSize, ushort method, ushort flags)> ScanZipLocalHeaders(FileStream fs)
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

        // 与 Preview 方法保持一致的权限检查
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        var isCreator = CurrentUser.Id.HasValue && CurrentUser.Id.Value == resource.CreatorId;
        if (!isApproved && !isCreator)
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
