using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.Conversion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

/// <summary>
/// 学生简历附件在线预览。
/// 附件存于 OSS，前端直接 window.open 公网 URL 会因 Content-Type 不对变成下载（Word 也无原生预览），
/// 走本端点统一保证内联预览：PDF / 图片直接返回，Word（doc/docx/rtf）经 Gotenberg 转 PDF 后返回。
/// 仅允许访问本应用 OSS Bucket 下 resumes/ 路径，防止成为开放代理。
/// </summary>
[Authorize(KnowledgeHubPermissions.Employment.ManageGuidance)]
[IgnoreAntiforgeryToken]
public class ResumePreviewController : AbpControllerBase
{
    private static readonly string[] InlineExtensions =
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly string[] ConvertibleExtensions =
    {
        ".doc", ".docx", ".rtf"
    };

    /// <summary>与上传上限一致，避免拉取超大文件打爆内存。</summary>
    private const long MaxFileSize = 10L * 1024 * 1024;

    private readonly IConfiguration _configuration;
    private readonly IOfficeConversionService _conversionService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ResumePreviewController(
        IConfiguration configuration,
        IOfficeConversionService conversionService,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _conversionService = conversionService;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    [Route("api/app/resume-preview")]
    public async Task<IActionResult> PreviewAsync([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "附件地址不合法" });
        }

        // 仅允许本应用 OSS Bucket 下的简历文件
        var bucketName = _configuration["Oss:BucketName"] ?? "kg-edu";
        var endpoint = _configuration["Oss:Endpoint"] ?? "oss-cn-beijing.aliyuncs.com";
        var region = endpoint.Replace(".aliyuncs.com", "").Replace("oss-", "");
        var expectedHost = $"{bucketName}.oss-{region}.aliyuncs.com";
        var uploadPath = (_configuration["Oss:UploadPath"] ?? "knowledgehub").Trim('/');

        if (!string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Contains($"/{uploadPath}/resumes/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "仅支持预览本站简历附件" });
        }

        var extension = Path.GetExtension(uri.AbsolutePath)?.ToLowerInvariant() ?? string.Empty;
        var isInline = InlineExtensions.Contains(extension);
        var isConvertible = ConvertibleExtensions.Contains(extension);
        if (!isInline && !isConvertible)
        {
            return BadRequest(new { message = "该附件类型不支持预览，请下载后查看" });
        }

        byte[] bytes;
        try
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            bytes = await http.GetByteArrayAsync(uri);
        }
        catch (Exception)
        {
            return NotFound(new { message = "附件不存在或已删除" });
        }

        if (bytes.Length == 0)
        {
            return NotFound(new { message = "附件不存在或已删除" });
        }

        if (bytes.Length > MaxFileSize)
        {
            return StatusCode(
                Microsoft.AspNetCore.Http.StatusCodes.Status413PayloadTooLarge,
                new { message = "附件过大，暂不支持在线预览，请下载后查看" });
        }

        if (isInline)
        {
            // 不带 Content-Disposition 即为内联预览
            return File(bytes, ToContentType(extension));
        }

        // Word → 经 Gotenberg 转 PDF。简历文件不可变（新上传即新 URL），
        // 用 URL 哈希做缓存 key，命中后毫秒级返回。
        var cacheKey = "resume-" + ToSha256Hex(url.Trim());
        var tempPath = Path.Combine(Path.GetTempPath(), $"resume-preview-{Guid.NewGuid():N}{extension}");
        try
        {
            await System.IO.File.WriteAllBytesAsync(tempPath, bytes);
            var pdfPath = await _conversionService.ConvertToPdfAsync(cacheKey, tempPath);
            return PhysicalFile(pdfPath, "application/pdf", enableRangeProcessing: true);
        }
        catch (OfficeConversionException ex)
        {
            Logger.LogWarning(ex, "[ResumePreview] 转换失败: {Url}", url);
            return StatusCode(
                Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                new { message = "附件转换失败，请下载后查看" });
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string ToContentType(string extension)
    {
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
    }

    private static string ToSha256Hex(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
