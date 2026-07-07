using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// 通过 HTTP multipart 调用本地 liteparse-server 容器的 /parse 端点。
/// 替代了原 .NET 解析流程（NPOI 解析 docx/pptx/xlsx，UglyToad.PdfPig 解析 pdf），
/// 现 PDF/DOCX/PPTX/XLSX 全部由 liteparse 内部 PDFium/Office 引擎处理。
/// </summary>
public class LiteParseDocumentExtractionService :
    IDocumentExtractionService,
    ILiteParseExtractionService,
    ITransientDependency
{
    private const string HttpClientName = "LiteParse";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LiteParseOptions> _options;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<LiteParseDocumentExtractionService> _logger;

    public LiteParseDocumentExtractionService(
        IHttpClientFactory httpClientFactory,
        IOptions<LiteParseOptions> options,
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter,
        ILogger<LiteParseDocumentExtractionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    /// <summary>
    /// 兼容 IDocumentExtractionService：只返回 PageContentDto 列表（不含 layout 信息）。
    /// </summary>
    public async Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId)
    {
        var result = await ExtractWithLayoutAsync(resourceId);
        return result.Pages;
    }

    /// <summary>
    /// 完整解析：返回 PageContentDto + PageWidths/PageHeights/TextItemsJson。
    /// </summary>
    public async Task<LiteParseExtractionResult> ExtractWithLayoutAsync(Guid resourceId)
    {
        var result = new LiteParseExtractionResult();

        try
        {
            // 用 FindAsync 而不是 GetQueryableAsync().Where()，避免 IQueryable 的 lazy DbContext 绑定
            // 在 BackgroundJob await HTTP 后被释放导致的 ObjectDisposedException。
            Resource? resource = null;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                resource = await _resourceRepository.FindAsync(resourceId);
            }

            if (resource == null)
            {
                _logger.LogWarning("LiteParse: Resource not found {ResourceId}", resourceId);
                return result;
            }

            if (string.IsNullOrEmpty(resource.FilePath))
            {
                _logger.LogWarning("LiteParse: Resource {ResourceId} has no FilePath", resourceId);
                return result;
            }

            var fullPath = Path.Combine(_fileStorageService.RootPath, resource.FilePath);
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("LiteParse: file not found {Path}", fullPath);
                return result;
            }

            var fileBytes = await File.ReadAllBytesAsync(fullPath);
            _logger.LogInformation(
                "LiteParse: parsing {Extension} {File} ({Size} bytes)",
                resource.FileExtension, fullPath, fileBytes.Length);

            var liteParseResponse = await CallLiteParseAsync(fileBytes, fullPath);

            if (liteParseResponse?.Pages == null || liteParseResponse.Pages.Count == 0)
            {
                _logger.LogWarning("LiteParse: empty result for resource {ResourceId}", resourceId);
                return result;
            }

            foreach (var page in liteParseResponse.Pages)
            {
                var text = page.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    // 跳过完全空白的页面，避免 Meili 端出现空 pageContent
                    continue;
                }

                result.Pages.Add(new PageContentDto
                {
                    PageNumber = page.PageNum,
                    Content = text,
                    Title = null
                });
                result.PageWidths.Add(page.Width);
                result.PageHeights.Add(page.Height);
                result.TextItemsJson.Add(page.TextItems != null && page.TextItems.Count > 0
                    ? JsonSerializer.Serialize(page.TextItems, JsonOpts)
                    : null);
            }

            _logger.LogInformation(
                "LiteParse: extracted {PageCount} pages for resource {ResourceId}",
                result.Pages.Count, resourceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiteParse: extraction failed for resource {ResourceId}", resourceId);
        }

        return result;
    }

    private async Task<LiteParseResponseDto?> CallLiteParseAsync(byte[] fileBytes, string fullPath, CancellationToken ct = default)
    {
        var http = _httpClientFactory.CreateClient(HttpClientName);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            GuessMimeType(Path.GetExtension(fullPath)));
        form.Add(fileContent, "file", Path.GetFileName(fullPath));

        var configJson = $"{{\"dpi\":{_options.Value.Dpi},\"ocrEnabled\":{_options.Value.OcrEnabled.ToString().ToLowerInvariant()}}}";
        form.Add(new StringContent(configJson, Encoding.UTF8, "application/json"), "config");

        using var response = await http.PostAsync("/parse", form, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "LiteParse: HTTP {Status} from {Host}/parse: {Body}",
                (int)response.StatusCode, _options.Value.Host, body);
            response.EnsureSuccessStatusCode();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<LiteParseResponseDto>(stream, JsonOpts, ct);
    }

    private static string GuessMimeType(string extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".doc" => "application/msword",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".xls" => "application/vnd.ms-excel",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}