using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

/// <summary>
/// 学生简历附件上传接口（PDF / Word / 图片）
/// 文件保存在后端 uploads 目录并返回公开访问 URL。
/// </summary>
[Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
[IgnoreAntiforgeryToken]
public class ResumeUploadController : AbpControllerBase
{
    private readonly IFileStorageService _fileStorageService;

    private static readonly string[] AllowedExtensions =
    {
        ".pdf", ".doc", ".docx", ".rtf",
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long MaxFileSize = 10L * 1024 * 1024; // 10 MB

    public ResumeUploadController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpPost]
    [Route("api/app/resume-upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
    public async Task<ResumeUploadResultDto> UploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("请选择要上传的简历文件");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("仅支持 PDF / Word / 图片 格式的简历文件");
        }

        if (file.Length > MaxFileSize)
        {
            throw new ArgumentException("简历文件大小不能超过 10MB");
        }

        var directory = Path.Combine("resumes", DateTime.Now.ToString("yyyyMMdd"));
        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();
        var relativePath = await _fileStorageService.SaveAsync(stream, storedFileName, directory);
        var url = _fileStorageService.GetFileUrl(relativePath);

        return new ResumeUploadResultDto
        {
            Url = url,
            FilePath = relativePath,
            OriginalFileName = file.FileName,
            Size = file.Length,
        };
    }
}

public class ResumeUploadResultDto
{
    public string Url { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public string OriginalFileName { get; set; } = default!;
    public long Size { get; set; }
}
