using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
    private readonly Resources.Conversion.PptxImagePreprocessor _pptxPreprocessor;
    private readonly ILogger<LiteParseDocumentExtractionService> _logger;

    public LiteParseDocumentExtractionService(
        IHttpClientFactory httpClientFactory,
        IOptions<LiteParseOptions> options,
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter,
        Resources.Conversion.PptxImagePreprocessor pptxPreprocessor,
        ILogger<LiteParseDocumentExtractionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _dataFilter = dataFilter;
        _pptxPreprocessor = pptxPreprocessor;
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

            // 大 PPTX 媒体预压缩：文本索引也优先用 light 文件，避免 liteparse 解析 200MB 原文件
            // 导致内存打爆/超时。light 生成失败时回退原文件。
            var parsePath = fullPath;
            if (_pptxPreprocessor.ShouldPreprocess(fullPath))
            {
                var light = await _pptxPreprocessor.GetOrCreateLightAsync(
                    resourceId.ToString(), fullPath);
                if (light != null)
                {
                    parsePath = light;
                    _logger.LogInformation(
                        "LiteParse: using preprocessed PPTX {ResourceId} -> {Light}",
                        resourceId, light);
                }
            }

            // 流式发送：不把整文件读进内存（200MB PPTX 直接 ReadAllBytes 会打爆内存）
            _logger.LogInformation(
                "LiteParse: parsing {Extension} {File} ({Size} bytes)",
                resource.FileExtension, parsePath, new FileInfo(parsePath).Length);

            LiteParseResponseDto? liteParseResponse = null;
            Exception? liteParseEx = null;
            try
            {
                liteParseResponse = await CallLiteParseAsync(parsePath);
            }
            catch (Exception ex)
            {
                liteParseEx = ex;
                _logger.LogWarning(ex, "LiteParse: primary parse failed for resource {ResourceId}, trying fallback", resourceId);
            }

            if (liteParseResponse?.Pages == null || liteParseResponse.Pages.Count == 0)
            {
                // 主路径失败或空结果：对 pptx 尝试 XML 备用提取（不依赖 LibreOffice / 中央目录）
                if (IsPptxFile(resource.FileExtension) || IsPptxFile(Path.GetExtension(parsePath)))
                {
                    var fallback = TryExtractPptxViaLocalHeaders(parsePath, resourceId);
                    if (fallback.Pages.Count > 0)
                    {
                        _logger.LogInformation("LiteParse: fallback XML extraction succeeded for resource {ResourceId} with {Count} pages", resourceId, fallback.Pages.Count);
                        return fallback;
                    }
                    if (liteParseEx != null)
                    {
                        _logger.LogWarning("LiteParse: fallback also empty for resource {ResourceId}", resourceId);
                    }
                }

                if (liteParseEx == null)
                {
                    _logger.LogWarning("LiteParse: empty result for resource {ResourceId}", resourceId);
                }
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

    private async Task<LiteParseResponseDto?> CallLiteParseAsync(string filePath, CancellationToken ct = default)
    {
        var http = _httpClientFactory.CreateClient(HttpClientName);

        using var form = new MultipartFormDataContent();
        // 流式上传：避免把大文件整体载入内存
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var _ = fileStream;
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            GuessMimeType(Path.GetExtension(filePath)));
        form.Add(fileContent, "file", Path.GetFileName(filePath));

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

    private static bool IsPptxFile(string? ext) =>
        string.Equals(ext, ".pptx", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// PPTX 备用提取：不依赖中央目录，直接遍历 Local File Header 逐条解压
    /// <c>ppt/slides/slide*.xml</c>，提取其中的 &lt;a:t&gt; 文本节点。
    /// 可在中央目录损坏、图片压缩数据损坏但幻灯片 XML 完好的情况下仍提取正文，
    /// 避免因 LibreOffice 转换失败导致整个文档索引为空。
    /// </summary>
    private LiteParseExtractionResult TryExtractPptxViaLocalHeaders(string filePath, Guid resourceId)
    {
        var result = new LiteParseExtractionResult();
        try
        {
            var slides = new SortedDictionary<int, string>();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            while (fs.Position + 30 <= fs.Length)
            {
                var sig = br.ReadUInt32();
                if (sig != 0x04034b50) // PK\003\004
                {
                    // 非 Local Header，尝试向前搜索下一个 PK 签名
                    fs.Position -= 3;
                    continue;
                }
                var version = br.ReadUInt16();
                var flag = br.ReadUInt16();
                var method = br.ReadUInt16();
                var modTime = br.ReadUInt16();
                var modDate = br.ReadUInt16();
                var crc32 = br.ReadUInt32();
                var compSize = br.ReadUInt32();
                var uncompSize = br.ReadUInt32();
                var fileNameLen = br.ReadUInt16();
                var extraLen = br.ReadUInt16();
                if (fs.Position + fileNameLen + extraLen > fs.Length) break;
                var fileNameBytes = br.ReadBytes(fileNameLen);
                var fileName = Encoding.UTF8.GetString(fileNameBytes);
                if (extraLen > 0) br.ReadBytes(extraLen);
                var dataStart = fs.Position;
                // 仅处理幻灯片 XML
                var isSlide = fileName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                              && fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
                if (isSlide)
                {
                    if (fs.Position + compSize > fs.Length)
                    {
                        _logger.LogWarning("LiteParse fallback: truncated entry {File}", fileName);
                        break;
                    }
                    var compData = br.ReadBytes((int)compSize);
                    string xml;
                    if (method == 0) // Stored
                    {
                        xml = Encoding.UTF8.GetString(compData);
                    }
                    else if (method == 8) // Deflated
                    {
                        try
                        {
                            using var compMs = new MemoryStream(compData);
                            using var ds = new DeflateStream(compMs, CompressionMode.Decompress);
                            using var outMs = new MemoryStream();
                            ds.CopyTo(outMs);
                            xml = Encoding.UTF8.GetString(outMs.ToArray());
                        }
                        catch (Exception dex)
                        {
                            _logger.LogWarning(dex, "LiteParse fallback: deflate failed for {File}", fileName);
                            continue;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("LiteParse fallback: unsupported method {Method} for {File}", method, fileName);
                        continue;
                    }
                    // 提取 <a:t> 文本
                    try
                    {
                        var xdoc = XDocument.Parse(xml);
                        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                        var texts = xdoc.Descendants(a + "t").Select(e => e.Value.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                        var content = string.Join("\n", texts);
                        // 从文件名提取页码：slide12.xml -> 12
                        var m = Regex.Match(fileName, @"slide(\d+)\.xml", RegexOptions.IgnoreCase);
                        var pageNum = m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : slides.Count + 1;
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            slides[pageNum] = content;
                        }
                    }
                    catch (Exception xex)
                    {
                        _logger.LogWarning(xex, "LiteParse fallback: XML parse failed for {File}", fileName);
                    }
                }
                else
                {
                    // 跳过非幻灯片数据
                    if (fs.Position + compSize > fs.Length) break;
                    fs.Position += compSize;
                }
            }

            foreach (var kv in slides.OrderBy(kv => kv.Key))
            {
                result.Pages.Add(new PageContentDto { PageNumber = kv.Key, Content = kv.Value, Title = null });
                result.PageWidths.Add(0);
                result.PageHeights.Add(0);
                result.TextItemsJson.Add(null);
            }
            if (result.Pages.Count > 0)
            {
                _logger.LogInformation("LiteParse fallback: extracted {Count} slides from {File}", result.Pages.Count, Path.GetFileName(filePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiteParse fallback: failed for resource {ResourceId}", resourceId);
        }
        return result;
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