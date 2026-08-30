using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

/// <summary>
/// 图片代理控制器：将远程图片（如 OSS 上的证书模板）通过本 API 转发给前端，
/// 使前端 HTML Canvas 能跨域绘制图片而不会污染画布（解决 OSS 未配置 CORS 的问题）。
///
/// 用法：
///   /api/image-proxy?url=https://bucket.oss-cn-beijing.aliyuncs.com/cert/xxx.png
/// </summary>
[Route("/api/image-proxy")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ImageProxyController : AbpControllerBase
{
    private const int MaxBytes = 32 * 1024 * 1024; // 限制 32MB，防止滥用

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageProxyController> _logger;

    public ImageProxyController(IHttpClientFactory httpClientFactory, ILogger<ImageProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 代理任意图片 URL（用于画布合成，前端通过此地址加载图片即可获得 CORS 许可）。
    /// </summary>
    /// <param name="url">远程图片完整 URL</param>
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || (!url.StartsWith("https://") && !url.StartsWith("http://")))
        {
            return BadRequest("无效的图片地址");
        }

        var client = _httpClientFactory.CreateClient("ImageProxy");
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var length = response.Content.Headers.ContentLength;
            if (length.HasValue && length.Value > MaxBytes)
            {
                return BadRequest("图片过大");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length > MaxBytes)
            {
                return BadRequest("图片过大");
            }

            return File(bytes, contentType);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "图片代理请求失败: {Url}", url);
            return StatusCode(502, "图片加载失败");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "图片代理请求超时: {Url}", url);
            return StatusCode(504, "图片加载超时");
        }
    }
}
