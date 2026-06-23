using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Controllers;

[Route("api/resource-file")]
public class ResourceFileController : AbpControllerBase
{
    protected IResourceRepository ResourceRepository { get; }
    protected IRepository<Resource, Guid> Repository { get; }
    protected IFileStorageService FileStorageService { get; }

    public ResourceFileController(
        IResourceRepository resourceRepository,
        IRepository<Resource, Guid> repository,
        IFileStorageService fileStorageService)
    {
        ResourceRepository = resourceRepository;
        Repository = repository;
        FileStorageService = fileStorageService;
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
        var resource = await ResourceRepository.GetWithDetailsAsync(resourceId);

        // Allow preview if resource is approved, OR if current user is the creator
        // (so uploaders can preview their own content before submitting for review)
        var isApproved = resource.Status == ResourceStatus.SchoolApproved ||
                         resource.Status == ResourceStatus.LeagueApproved;
        var isCreator = CurrentUser.Id.HasValue && CurrentUser.Id.Value == resource.CreatorId;
        
        if (!isApproved && !isCreator)
        {
            return Forbid();
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

        // PhysicalFile supports EnableRangeProcessing for chunked download
        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
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
