using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
            using var archive = ZipFile.OpenRead(fullPath);
            var count = archive.Entries.Count(e =>
                Regex.IsMatch(e.FullName, @"^ppt/slides/slide\d+\.xml$"));
            return new JsonResult(new { count });
        }
        catch
        {
            return BadRequest(new { message = "无法读取 PPTX 文件" });
        }
    }

    /// <summary>
    /// 获取 PPTX 单张幻灯片的文本内容（按需提取，不下载整个文件）
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
            using var archive = ZipFile.OpenRead(fullPath);
            var entryPath = $"ppt/slides/slide{slideNumber}.xml";
            var entry = archive.GetEntry(entryPath);
            if (entry == null)
                return NotFound(new { message = $"幻灯片 {slideNumber} 不存在" });

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var texts = new List<SlideTextDto>();
            foreach (var tEl in doc.Descendants(ns + "t"))
            {
                var text = tEl.Value;
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Walk up to find formatting (rPr)
                int fontSize = 18;
                bool bold = false;
                string color = "#333333";

                var rPr = tEl.Parent?.Element(ns + "rPr");
                if (rPr != null)
                {
                    var sz = rPr.Attribute("sz");
                    if (sz != null && int.TryParse(sz.Value, out var szVal))
                        fontSize = szVal / 100;
                    bold = rPr.Element(ns + "b") != null;
                    var solidFill = rPr.Element(ns + "solidFill");
                    if (solidFill != null)
                    {
                        var srgb = solidFill.Element(ns + "srgbClr");
                        if (srgb != null)
                        {
                            var val = srgb.Attribute("val");
                            if (val != null) color = "#" + val.Value;
                        }
                    }
                }

                texts.Add(new SlideTextDto
                {
                    Text = text,
                    FontSize = fontSize,
                    Bold = bold,
                    Color = color
                });
            }

            return new JsonResult(new { slideNumber, texts });
        }
        catch
        {
            return BadRequest(new { message = "幻灯片提取失败" });
        }
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
