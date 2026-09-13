using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Volo.Abp.AspNetCore.Mvc;
using Aliyun.OSS;

namespace KnowledgeHub.Controllers;

/// <summary>
/// OSS 上传控制器。上传会写入生产对象存储并产生费用，必须登录且具备资源创建权限。
/// </summary>
[Authorize(KnowledgeHubPermissions.Resources.Create)]
[IgnoreAntiforgeryToken]
public class OssUploadController : AbpControllerBase
{
    private readonly IConfiguration _configuration;

    public OssUploadController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    /// <summary>
    /// 通用文件上传白名单：仅允许文档 / 媒体 / 压缩包，禁止可执行与可脚本化类型（html/js/svg 等）。
    /// </summary>
    private static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".md", ".csv", ".json", ".xml",
        ".zip", ".rar", ".7z",
        ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv",
        ".mp3", ".wav", ".m4a", ".aac", ".flac",
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private static readonly Dictionary<string, string> ContentTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
        [".7z"] = "application/x-7z-compressed",
        [".mp4"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".avi"] = "video/x-msvideo",
        [".mkv"] = "video/x-matroska",
        [".webm"] = "video/webm",
        [".flv"] = "video/x-flv",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".flac"] = "audio/flac",
    };

    [HttpPost]
    [Route("api/oss-upload/image")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 10485760)] // 10MB
    public async Task<OssUploadResultDto> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("请选择要上传的图片");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension))
        {
            throw new ArgumentException("仅支持上传 JPG、PNG、GIF、WebP、BMP 格式的图片");
        }

        // Validate file size (max 10MB)
        if (file.Length > 10 * 1024 * 1024)
        {
            throw new ArgumentException("图片大小不能超过 10MB");
        }

        // 图片保持原路径（Oss:UploadPath/images/{date}/{name}），不要重命名以免历史 URL 失效。
        return await UploadToOssAsync(file, subDir: null);
    }

    /// <summary>
    /// 通用文件上传 — 供实训资料、章节目录、模板等场景使用。
    /// 仅允许文档 / 媒体 / 压缩包白名单，文件大小限制 50MB；返回公开 URL。
    /// </summary>
    [HttpPost]
    [Route("api/oss-upload/file")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB
    public async Task<OssUploadResultDto> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("请选择要上传的文件");
        }
        if (file.Length > 50 * 1024 * 1024)
        {
            throw new ArgumentException("文件大小不能超过 50MB");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedFileExtensions.Contains(extension))
        {
            throw new ArgumentException("不支持的文件类型");
        }

        // 通用文件落到 Oss:UploadPath/files/{date}/{name}，与图片分目录。
        return await UploadToOssAsync(file, subDir: "files");
    }

    /// <summary>
    /// 实际执行 OSS 上传。subDir 为 null 时直接拼到 uploadPath 后面（保持向后兼容）；
    /// 否则多一层子目录（用于文件 / 视频 / 音频 等大文件分类）。
    /// </summary>
    private async Task<OssUploadResultDto> UploadToOssAsync(IFormFile file, string? subDir)
    {
        var endpoint = _configuration["Oss:Endpoint"] ?? "oss-cn-beijing.aliyuncs.com";
        var accessKeyId = _configuration["Oss:AccessKeyId"] ?? "";
        var accessKeySecret = _configuration["Oss:AccessKeySecret"] ?? "";
        var bucketName = _configuration["Oss:BucketName"] ?? "kg-edu";
        var uploadPath = _configuration["Oss:UploadPath"] ?? "knowledgehub";

        var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

        // Generate unique object key
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension)) extension = ".bin";
        var datePrefix = DateTime.Now.ToString("yyyyMMdd");
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var objectKey = string.IsNullOrEmpty(subDir)
            ? $"{uploadPath}/{datePrefix}/{uniqueName}"
            : $"{uploadPath}/{subDir}/{datePrefix}/{uniqueName}";

        // 不信任客户端提供的 ContentType，按扩展名推断，禁止 text/html 等可执行类型
        var contentType = ContentTypeMap.TryGetValue(extension, out var mapped)
            ? mapped
            : "application/octet-stream";

        using var stream = file.OpenReadStream();
        var metadata = new ObjectMetadata
        {
            ContentType = contentType,
            CacheControl = "max-age=31536000", // 1 year cache
        };

        await Task.Run(() => client.PutObject(bucketName, objectKey, stream, metadata));

        var region = endpoint.Replace(".aliyuncs.com", "").Replace("oss-", "");
        var url = $"https://{bucketName}.oss-{region}.aliyuncs.com/{objectKey}";

        return new OssUploadResultDto
        {
            Url = url,
            ObjectKey = objectKey,
            OriginalFileName = file.FileName,
            Size = file.Length,
        };
    }
}

public class OssUploadResultDto
{
    public string Url { get; set; } = default!;
    public string ObjectKey { get; set; } = default!;
    public string OriginalFileName { get; set; } = default!;
    public long Size { get; set; }
}
