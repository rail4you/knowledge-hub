using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Volo.Abp.AspNetCore.Mvc;
using Aliyun.OSS;

namespace KnowledgeHub.Controllers;

[IgnoreAntiforgeryToken]
public class OssUploadController : AbpControllerBase
{
    private readonly IConfiguration _configuration;

    public OssUploadController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

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

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp" };
        if (Array.IndexOf(allowedTypes, file.ContentType.ToLowerInvariant()) < 0)
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
    /// 通用文件上传（不限制类型）— 供实训资料、章节目录、模板等场景使用。
    /// 文件大小限制 50MB；返回公开 URL。
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

        using var stream = file.OpenReadStream();
        var metadata = new ObjectMetadata
        {
            ContentType = file.ContentType,
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
