using System;
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
/// 学生简历附件上传接口（PDF / Word / 图片）
/// 上传到 OSS 并返回公开访问 URL。
/// </summary>
[Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
[IgnoreAntiforgeryToken]
public class ResumeUploadController : AbpControllerBase
{
    private readonly IConfiguration _configuration;

    private static readonly string[] AllowedExtensions =
    {
        ".pdf", ".doc", ".docx", ".rtf",
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long MaxFileSize = 10L * 1024 * 1024; // 10 MB

    public ResumeUploadController(IConfiguration configuration)
    {
        _configuration = configuration;
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

        var endpoint = _configuration["Oss:Endpoint"] ?? "oss-cn-beijing.aliyuncs.com";
        var accessKeyId = _configuration["Oss:AccessKeyId"] ?? "";
        var accessKeySecret = _configuration["Oss:AccessKeySecret"] ?? "";
        var bucketName = _configuration["Oss:BucketName"] ?? "kg-edu";
        var uploadPath = _configuration["Oss:UploadPath"] ?? "knowledgehub";

        var client = new OssClient(endpoint, accessKeyId, accessKeySecret);

        var datePrefix = DateTime.Now.ToString("yyyyMMdd");
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var objectKey = $"{uploadPath}/resumes/{datePrefix}/{uniqueName}";

        using var stream = file.OpenReadStream();
        var metadata = new ObjectMetadata
        {
            ContentType = file.ContentType,
            CacheControl = "max-age=31536000",
        };

        await Task.Run(() => client.PutObject(bucketName, objectKey, stream, metadata));

        var region = endpoint.Replace(".aliyuncs.com", "").Replace("oss-", "");
        var url = $"https://{bucketName}.oss-{region}.aliyuncs.com/{objectKey}";

        return new ResumeUploadResultDto
        {
            Url = url,
            FilePath = objectKey,
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
