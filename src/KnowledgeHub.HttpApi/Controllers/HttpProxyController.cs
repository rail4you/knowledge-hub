using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Controllers;

/// <summary>
/// HTTP 代理控制器：将 /api/proxy/http/{host}/{**path} 转发到 http://{host}/{path}，
/// 用于解决 HTTPS 页面中无法加载 HTTP iframe 的 Mixed Content 问题。
///
/// 安全约束（防 SSRF）：
///   1. 必须登录（[Authorize]），匿名不可用；
///   2. 配置 HttpProxy:AllowedHosts 时，仅放行白名单内的 host（精确匹配，支持 host 或 host:port）；
///   3. 未配置白名单时，仅允许解析到公网地址的 host，阻断回环/私有/链路本地/保留地址。
///
/// 用法：
///   原始 URL:  http://124.222.92.99:8003/anatomyMice/?messageKey=xxx
///   代理 URL:  /api/proxy/http/124.222.92.99:8003/anatomyMice/?messageKey=xxx
/// </summary>
[Route("/api/proxy/http")]
[Authorize]
[IgnoreAntiforgeryToken]
public class HttpProxyController : KnowledgeHubController
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpProxyController> _logger;

    public HttpProxyController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HttpProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 代理任意 HTTP GET 请求
    /// </summary>
    /// <param name="host">目标 host，可含端口号，如 124.222.92.99:8003</param>
    /// <param name="path">目标路径，如 anatomyMice/</param>
    [HttpGet("{host}/{**path}")]
    public async Task<IActionResult> ProxyGetAsync(string host, string path)
    {
        return await ProxyAsync(host, path, HttpMethod.Get);
    }

    /// <summary>
    /// 代理任意 HTTP POST 请求（Unity WebGL 可能需要 POST 加载 data 文件）
    /// </summary>
    [HttpPost("{host}/{**path}")]
    public async Task<IActionResult> ProxyPostAsync(string host, string path)
    {
        return await ProxyAsync(host, path, HttpMethod.Post);
    }

    private async Task<IActionResult> ProxyAsync(string host, string path, HttpMethod method)
    {
        if (!ProxyHostGuard.IsHostAllowed(host, _configuration["HttpProxy:AllowedHosts"]))
        {
            _logger.LogWarning("HttpProxy 拒绝访问未授权 host: {Host}", host);
            return StatusCode(StatusCodes.Status403Forbidden, "代理目标不被允许");
        }

        var queryString = Request.QueryString.Value ?? string.Empty;
        var targetUrl = $"http://{host}/{path}{queryString}";

        var client = _httpClientFactory.CreateClient("HttpProxy");
        using var request = new HttpRequestMessage(method, targetUrl);

        // 透传 Accept / Accept-Encoding / Range 等关键头
        if (Request.Headers.TryGetValue("Accept", out var accept))
            request.Headers.TryAddWithoutValidation("Accept", accept.ToString());
        if (Request.Headers.TryGetValue("Accept-Encoding", out var acceptEncoding))
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding.ToString());
        if (Request.Headers.TryGetValue("Range", out var range))
            request.Headers.TryAddWithoutValidation("Range", range.ToString());

        // POST body 透传
        if (method == HttpMethod.Post)
        {
            request.Content = new StreamContent(Request.Body);
            if (Request.Headers.TryGetValue("Content-Type", out var contentType))
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType.ToString());
        }

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // 透传 Content-Type 和关键响应头
            Response.StatusCode = (int)response.StatusCode;
            if (response.Content.Headers.ContentType != null)
                Response.Headers["Content-Type"] = response.Content.Headers.ContentType.ToString();
            if (response.Content.Headers.ContentLength.HasValue)
                Response.Headers["Content-Length"] = response.Content.Headers.ContentLength.Value.ToString();
            if (response.Headers.TryGetValues("Content-Encoding", out var ce))
                Response.Headers["Content-Encoding"] = string.Join(",", ce);
            if (response.Headers.TryGetValues("Accept-Ranges", out var ar))
                Response.Headers["Accept-Ranges"] = string.Join(",", ar);

            // 流式转发 body
            using var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(Response.Body);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, "代理请求失败：无法连接到目标服务器");
        }

        return new EmptyResult();
    }
}
