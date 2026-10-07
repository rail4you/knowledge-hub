using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Employment;
using KnowledgeHub.Employment.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Controllers;

/// <summary>
/// 招聘岗位宣传视频：上传（登录 + PublishJob 权限）与播放（匿名，仅已发布岗位）。
/// 存储走本地 IFileStorageService（目录 job-videos/），播放走 /api 代理（Range 支持）。
/// </summary>
[IgnoreAntiforgeryToken]
public class JobVideoController : AbpControllerBase
{
    private const string VideoDirectory = "job-videos";
    private const long MaxVideoSize = 500L * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
    {
        ".mp4", ".webm", ".ogg", ".mov", ".m4v"
    };

    private readonly IFileStorageService _fileStorageService;
    private readonly IRepository<JobPosting, Guid> _jobPostingRepository;
    private readonly IDataFilter _dataFilter;

    public JobVideoController(
        IFileStorageService fileStorageService,
        IRepository<JobPosting, Guid> jobPostingRepository,
        IDataFilter dataFilter)
    {
        _fileStorageService = fileStorageService;
        _jobPostingRepository = jobPostingRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>上传岗位视频，返回相对存储路径（如 job-videos/xxx.mp4），前端随岗位表单一并保存。</summary>
    [HttpPost]
    [Route("api/app/job-video/upload")]
    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    [RequestSizeLimit(MaxVideoSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<JobVideoUploadResultDto> UploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("请选择要上传的视频文件。");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensions.Contains(extension))
        {
            throw new UserFriendlyException("仅支持 MP4 / WebM / OGG / MOV / M4V 格式的视频。");
        }

        if (file.Length > MaxVideoSize)
        {
            throw new UserFriendlyException("视频大小不能超过 500MB。");
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        var path = await _fileStorageService.SaveAsync(stream, fileName, VideoDirectory);

        return new JobVideoUploadResultDto
        {
            Url = path.Replace("\\", "/"),
            OriginalFileName = file.FileName,
            Size = file.Length,
        };
    }

    /// <summary>
    /// 播放岗位视频（匿名可访问，仅限已发布岗位的视频，支持 Range 拖动播放）。
    /// </summary>
    [HttpGet]
    [Route("api/app/job-video")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync([FromQuery] string path)
    {
        var relative = ValidatePath(path);
        if (relative == null)
        {
            return BadRequest(new { message = "视频路径不合法" });
        }

        // 该视频必须挂在一个「已发布」岗位上，防止内部文件被枚举外泄。
        JobPosting? job;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            job = await (await _jobPostingRepository.GetQueryableAsync())
                .FirstOrDefaultAsync(x => x.VideoUrl == relative);
        }

        if (job == null || job.Status != EmploymentJobStatus.Published)
        {
            return NotFound();
        }

        var fullPath = Path.GetFullPath(Path.Combine(_fileStorageService.RootPath, relative));
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, ToContentType(Path.GetExtension(fullPath)), enableRangeProcessing: true);
    }

    /// <summary>管理端预览：岗位所有人或审核人可播放未发布岗位的视频。</summary>
    [HttpGet]
    [Route("api/app/job-video/preview")]
    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task<IActionResult> PreviewAsync([FromQuery] string path)
    {
        var relative = ValidatePath(path);
        if (relative == null)
        {
            return BadRequest(new { message = "视频路径不合法" });
        }

        JobPosting? job;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            job = await (await _jobPostingRepository.GetQueryableAsync())
                .FirstOrDefaultAsync(x => x.VideoUrl == relative);
        }

        if (job == null)
        {
            return NotFound();
        }

        var isEmployer = job.EmployerUserId == CurrentUser.Id;
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ReviewJob);
        var isTenantAdmin = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ManageResume);
        if (!isEmployer && !canReview && !isTenantAdmin)
        {
            return NotFound();
        }

        var fullPath = Path.GetFullPath(Path.Combine(_fileStorageService.RootPath, relative));
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, ToContentType(Path.GetExtension(fullPath)), enableRangeProcessing: true);
    }

    private static string? ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relative = path.Trim().Replace("\\", "/");
        if (!relative.StartsWith($"{VideoDirectory}/", StringComparison.Ordinal) ||
            relative.Contains("..", StringComparison.Ordinal) ||
            relative.Contains('\0'))
        {
            return null;
        }

        return relative;
    }

    private static string ToContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream",
        };
    }
}

public class JobVideoUploadResultDto
{
    public string Url { get; set; } = default!;
    public string OriginalFileName { get; set; } = default!;
    public long Size { get; set; }
}
