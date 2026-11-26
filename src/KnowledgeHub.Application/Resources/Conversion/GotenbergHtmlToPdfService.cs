using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client;

namespace KnowledgeHub.Resources.Conversion;

/// <inheritdoc />
public class GotenbergHtmlToPdfService : IHtmlToPdfService, ITransientDependency
{
    private const string HtmlRoute = "/forms/chromium/convert/html";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<GotenbergHtmlToPdfService> _logger;

    public GotenbergHtmlToPdfService(
        IHttpClientFactory httpClientFactory,
        Microsoft.Extensions.Options.IOptions<OfficeConversionOptions> options,
        ILogger<GotenbergHtmlToPdfService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new BusinessException("打印内容为空。");
        }

        var client = _httpClientFactory.CreateClient(GotenbergConversionService.GotenbergHttpClientName);

        using var form = new MultipartFormDataContent();

        // Gotenberg 要求主文件名为 index.html
        var htmlContent = new StringContent(html, Encoding.UTF8, "text/html");
        form.Add(htmlContent, "files", "index.html");

        // 边距 12mm；printBackground 让表头底色等样式生效。
        // ⚠️ 不要传 paperWidth/paperHeight 的 "A4"：Gotenberg 这两个字段只接受**数值（英寸）**，
        //    传 "A4" 会直接 400（Invalid form data: paperWidth ... ParseFloat）。
        //    纸张尺寸改用 CSS 的 @page { size: A4 portrait } 声明（见 `ExamPrintHtmlBuilder`）。
        form.Add(new StringContent("12mm"), "marginTop");
        form.Add(new StringContent("12mm"), "marginBottom");
        form.Add(new StringContent("12mm"), "marginLeft");
        form.Add(new StringContent("12mm"), "marginRight");
        form.Add(new StringContent("true"), "printBackground");

        var url = _options.BaseUrl.TrimEnd('/') + HtmlRoute;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _options.ConversionTimeoutSeconds)));

        try
        {
            using var response = await client.PostAsync(url, form, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                _logger.LogError("[HtmlToPdf] Gotenberg {Status}: {Body}", (int)response.StatusCode, body);
                throw new BusinessException($"打印失败（Gotenberg {(int)response.StatusCode}）。");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                // 出错时 Gotenberg 会返回 text/plain，避免把错误页当 PDF 交给用户
                var text = await response.Content.ReadAsStringAsync(timeout.Token);
                _logger.LogError("[HtmlToPdf] 非 PDF 响应 {MediaType}: {Text}", mediaType, text);
                throw new BusinessException("打印失败：转换服务返回了非 PDF 内容。");
            }

            return await response.Content.ReadAsByteArrayAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("[HtmlToPdf] 转换超时 {Seconds}s", _options.ConversionTimeoutSeconds);
            throw new BusinessException("打印超时，请稍后重试。");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[HtmlToPdf] 无法连接 Gotenberg: {Url}", url);
            throw new BusinessException("打印服务不可用（Gotenberg 未启动？）。");
        }
    }
}
