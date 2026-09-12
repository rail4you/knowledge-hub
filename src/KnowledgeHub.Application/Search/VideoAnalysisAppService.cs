using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

// 视频理解会调用付费的 Qwen VL API，且时间轴会写入共享的 videos 索引。
// 该服务仅供后台索引任务内部调用和管理端调试，必须限制为索引管理权限，
// 禁止匿名/普通用户调用以免刷额度或污染他人索引。
[Authorize(KnowledgeHubPermissions.Search.ManageIndex)]
public class VideoAnalysisAppService : KnowledgeHubAppService, IVideoAnalysisAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoAnalysisAppService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFfmpegRunner _ffmpegRunner;
    private readonly OfficeConversionOptions _conversionOptions;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentTenant _currentTenant;

    private const string DefaultModel = "qwen3-vl-flash";
    /// <summary>视频抽帧率（帧/秒）：时间轴分析 0.2（5 秒一帧）足够，烧钱量与帧数成正比，可用 Qwen:VideoFps 覆盖。</summary>
    private const double DefaultFps = 0.2;
    private const string VideosIndexName = "videos";
    private const string TimelinePrompt = @"请详细分析这段视频的内容，按照时间轴输出事件列表。

要求：
1. 以 JSON 格式输出
2. 包含 events 数组
3. 每个事件包含 start_time（开始时间，HH:mm:ss 格式）、end_time（结束时间，HH:mm:ss 格式）、event（事件描述）
4. 事件描述要详细，包含人物动作、场景变化等
5. 不要输出任何其他内容，只输出 JSON

输出格式示例：
{""events"": [{""start_time"": ""00:00:00"", ""end_time"": ""00:00:05"", ""event"": ""场景描述""}]}";

    public VideoAnalysisAppService(
        IConfiguration configuration,
        ILogger<VideoAnalysisAppService> logger,
        IHttpClientFactory httpClientFactory,
        IFfmpegRunner ffmpegRunner,
        IOptions<OfficeConversionOptions> conversionOptions,
        IFileStorageService fileStorageService,
        ICurrentTenant currentTenant)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _ffmpegRunner = ffmpegRunner;
        _conversionOptions = conversionOptions.Value;
        _fileStorageService = fileStorageService;
        _currentTenant = currentTenant;
    }

    public async Task<VideoAnalysisResultDto> AnalyzeVideoTimelineAsync(VideoAnalysisRequestDto input)
    {
        if (string.IsNullOrWhiteSpace(input.FilePath) && string.IsNullOrWhiteSpace(input.VideoUrl))
        {
            throw new UserFriendlyException("Must provide FilePath or VideoUrl");
        }

        string videoDataUrl;
        if (!string.IsNullOrWhiteSpace(input.VideoUrl))
        {
            videoDataUrl = input.VideoUrl;
        }
        else
        {
            videoDataUrl = await PrepareLocalVideoAsync(input.FilePath!);
        }

        var prompt = string.IsNullOrWhiteSpace(input.CustomPrompt) ? TimelinePrompt : input.CustomPrompt;

        // 与 Chat 调用共用全局 Qwen 并发限流，避免视频理解请求打爆配额
        using var lease = await QwenClient.AcquireAsync(_configuration);
        return await CallQwenVlApiAsync(videoDataUrl, prompt);
    }

    public async Task<VideoAnalysisResultDto> AnalyzeLocalVideoAsync(string filePath)
    {
        return await AnalyzeVideoTimelineAsync(new VideoAnalysisRequestDto { FilePath = filePath });
    }

    public async Task SaveVideoTimelineToMeiliSearchAsync(Guid videoId, string videoName, string videoUrl, VideoAnalysisResultDto analysisResult)
    {
        await EnsureVideosIndexExistsAsync();

        // 租户隔离：把当前租户写入每条视频时间轴文档。
        // Host 用户（CurrentTenant 为空）写空字符串；租户用户严格只搜到本租户视频，Host 视频对租户不可见。
        var tenantId = _currentTenant.Id?.ToString() ?? "";

        var documents = analysisResult.Events.Select((evt, index) => new
        {
            id = $"{videoId}_{index}",
            resourceId = videoId.ToString(),
            videoId = videoId.ToString(),
            videoName = videoName,
            videoUrl = videoUrl,
            startTime = evt.StartTime,
            endTime = evt.EndTime,
            eventDescription = evt.Event,
            order = index,
            tenantId = tenantId,
            indexedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        }).ToList();

        var json = JsonSerializer.Serialize(documents, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var client = _httpClientFactory.CreateClient("MeiliSearch");
        client.BaseAddress = new Uri(_configuration["Meilisearch:Host"] ?? "http://localhost:7700");
        var apiKey = _configuration["Meilisearch:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var response = await client.PostAsync(
            $"/indexes/{VideosIndexName}/documents",
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to save video timeline to Meilisearch: {StatusCode} - {Body}", response.StatusCode, errorBody);
            throw new UserFriendlyException($"Failed to save video timeline: {response.StatusCode}");
        }

        _logger.LogInformation("Saved {Count} timeline events for video {VideoId} to Meilisearch", documents.Count, videoId);
    }

    private async Task EnsureVideosIndexExistsAsync()
    {
        var client = _httpClientFactory.CreateClient("MeiliSearch");
        client.BaseAddress = new Uri(_configuration["Meilisearch:Host"] ?? "http://localhost:7700");
        var apiKey = _configuration["Meilisearch:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var checkResponse = await client.GetAsync($"/indexes/{VideosIndexName}");
        if (checkResponse.IsSuccessStatusCode)
        {
            // 已存在也需同步设置：历史索引可能是 wildcard searchable-attributes ("*")，导致数字查询 "1"
            // 误匹配到 id/order 等隐藏字段而产生 1.0 满分（见 https://localhost:4200/search?q=1 复现）。
            var indexBaseExisting = client.BaseAddress + $"/indexes/{VideosIndexName}";
            try
            {
                await client.PutAsJsonAsync($"{indexBaseExisting}/settings/filterable-attributes",
                    new[] { "resourceId", "videoId", "videoName", "tenantId", "indexedAt" });
            }
            catch { }
            try
            {
                await client.PutAsJsonAsync($"{indexBaseExisting}/settings/searchable-attributes",
                    new[] { "videoName", "eventDescription" });
            }
            catch { }
            try
            {
                await client.PutAsJsonAsync($"{indexBaseExisting}/settings/sortable-attributes",
                    new[] { "order", "indexedAt", "startTime" });
            }
            catch { }
            // 同步 ranking-rules，保持与 documents 索引一致的 words 优先策略
            try
            {
                await client.PutAsJsonAsync($"{indexBaseExisting}/settings/ranking-rules", new[]
                {
                    "words", "typo", "proximity", "attribute", "sort", "exactness"
                });
            }
            catch { }
            return;
        }

        _logger.LogInformation("Creating videos index in Meilisearch");

        var createContent = new { uid = VideosIndexName, primaryKey = "id" };
        var createResponse = await client.PostAsJsonAsync("/indexes", createContent);
        createResponse.EnsureSuccessStatusCode();

        var indexBase = client.BaseAddress + $"/indexes/{VideosIndexName}";

        await client.PutAsJsonAsync($"{indexBase}/settings/filterable-attributes",
            new[] { "resourceId", "videoId", "videoName", "tenantId", "indexedAt" });

        await client.PutAsJsonAsync($"{indexBase}/settings/searchable-attributes",
            new[] { "videoName", "eventDescription" });

        await client.PutAsJsonAsync($"{indexBase}/settings/sortable-attributes",
            new[] { "order", "indexedAt", "startTime" });

        await client.PutAsJsonAsync($"{indexBase}/settings/ranking-rules", new[]
        {
            "words", "typo", "proximity", "attribute", "sort", "exactness"
        });

        _logger.LogInformation("Videos index created successfully");
    }

    private async Task<string> PrepareLocalVideoAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new UserFriendlyException($"Video file not found: {filePath}");
        }

        // 优先返回公开 URL，避免把整个视频 base64 内联进请求体（内存/带宽浪费）。
        // 仅当文件位于上传目录且 App:SelfUrl 为非 localhost 的公网地址时可用；
        // 生产索引流程本身已走 URL 路径，这里覆盖直接的本地调用场景。
        var publicUrl = TryBuildPublicUrl(filePath);
        if (publicUrl != null)
        {
            _logger.LogInformation("Using public URL for local video instead of base64: {Url}", publicUrl);
            return publicUrl;
        }

        var fileInfo = new FileInfo(filePath);
        var extension = fileInfo.Extension.ToLowerInvariant();
        string targetPath = filePath;

        if (extension != ".mp4")
        {
            _logger.LogInformation("Converting video format {Extension} to mp4", extension);
            targetPath = await ConvertToMp4Async(filePath);
        }

        var targetInfo = new FileInfo(targetPath);
        _logger.LogInformation("Video file: {Path}, size: {Size:F2} MB", targetPath, targetInfo.Length / 1024.0 / 1024.0);

        if (targetInfo.Length > 7 * 1024 * 1024)
        {
            _logger.LogInformation("Video too large, compressing...");
            targetPath = await CompressVideoAsync(targetPath);
            targetInfo = new FileInfo(targetPath);
            _logger.LogInformation("Compressed size: {Size:F2} MB", targetInfo.Length / 1024.0 / 1024.0);
        }

        if (targetInfo.Length > 7 * 1024 * 1024)
        {
            throw new UserFriendlyException(
                $"Video file too large ({targetInfo.Length / 1024.0 / 1024.0:F2} MB). " +
                "Please provide a public URL or compress the video.");
        }

        var bytes = await File.ReadAllBytesAsync(targetPath);
        var base64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:video/mp4;base64,{base64}";

        if (targetPath != filePath && File.Exists(targetPath))
        {
            try { File.Delete(targetPath); } catch { }
        }

        return dataUrl;
    }

    /// <summary>
    /// 若文件位于上传根目录且 App:SelfUrl 为公网地址，构造其公开 URL；否则返回 null。
    /// </summary>
    private string? TryBuildPublicUrl(string filePath)
    {
        var selfUrl = _configuration["App:SelfUrl"];
        if (string.IsNullOrWhiteSpace(selfUrl) || IsLocalhostUrl(selfUrl))
        {
            return null;
        }

        try
        {
            var fullRoot = Path.GetFullPath(_fileStorageService.RootPath);
            var full = Path.GetFullPath(filePath);
            if (!full.StartsWith(fullRoot, StringComparison.Ordinal))
            {
                return null;
            }

            var relative = Path.GetRelativePath(fullRoot, full).Replace('\\', '/');
            var encoded = string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));
            return $"{selfUrl.TrimEnd('/')}/uploads/{encoded}";
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLocalhostUrl(string url)
    {
        return url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || url.Contains("127.0.0.1")
            || url.Contains("::1");
    }

    private async Task<string> ConvertToMp4Async(string inputPath)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"video_{Guid.NewGuid()}.mp4");

        var args = new List<string>
        {
            "-y",
            "-i", inputPath,
            "-c:v", "libx264",
            "-crf", "28",
            "-preset", "veryfast",
            "-threads", Math.Max(1, _conversionOptions.FfmpegThreads).ToString(),
            outputPath
        };

        var result = await _ffmpegRunner.RunFfmpegAsync(args, TimeSpan.FromMinutes(10));
        if (!result.Success)
        {
            throw new UserFriendlyException($"Video conversion failed: {result.StandardError}");
        }

        return outputPath;
    }

    private async Task<string> CompressVideoAsync(string inputPath)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"video_compressed_{Guid.NewGuid()}.mp4");

        var args = new List<string>
        {
            "-y",
            "-i", inputPath,
            "-c:v", "libx264",
            "-crf", "32",
            "-preset", "veryfast",
            "-vf", "scale=-2:480",
            // 视觉理解不需要音轨
            "-an",
            "-threads", Math.Max(1, _conversionOptions.FfmpegThreads).ToString(),
            outputPath
        };

        var result = await _ffmpegRunner.RunFfmpegAsync(args, TimeSpan.FromMinutes(10));
        if (!result.Success)
        {
            _logger.LogWarning("Video compression failed: {Error}, using original", result.StandardError);
            return inputPath;
        }

        return outputPath;
    }

    private async Task<VideoAnalysisResultDto> CallQwenVlApiAsync(string videoUrl, string? prompt = null)
    {
        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new UserFriendlyException("Qwen:ApiKey not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:VisionModel"] ?? DefaultModel;
        var textPrompt = prompt ?? TimelinePrompt;
        var fps = _configuration.GetValue("Qwen:VideoFps", DefaultFps);
        if (fps <= 0) fps = DefaultFps;

        _logger.LogInformation("Calling Qwen VL API, model: {Model}", model);

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "video_url",
                            video_url = new { url = videoUrl },
                            fps
                        },
                        new
                        {
                            type = "text",
                            text = textPrompt
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Qwen VL API failed: {StatusCode} - {Body}", response.StatusCode, responseBody);
            throw new UserFriendlyException($"Video analysis API call failed: {response.StatusCode}");
        }

        return ParseResponse(responseBody);
    }

    private VideoAnalysisResultDto ParseResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                ?? throw new UserFriendlyException("API returned empty content");

            _logger.LogDebug("Qwen VL raw response: {Content}", content);

            var jsonContent = ExtractJson(content);
            var events = new List<VideoTimelineEventDto>();

            using var eventsDoc = JsonDocument.Parse(jsonContent);
            var eventsArray = eventsDoc.RootElement.GetProperty("events");

            foreach (var evt in eventsArray.EnumerateArray())
            {
                events.Add(new VideoTimelineEventDto
                {
                    StartTime = evt.GetProperty("start_time").GetString() ?? "",
                    EndTime = evt.GetProperty("end_time").GetString() ?? "",
                    Event = evt.GetProperty("event").GetString() ?? ""
                });
            }

            VideoAnalysisUsageDto? usage = null;
            if (root.TryGetProperty("usage", out var usageElement))
            {
                usage = new VideoAnalysisUsageDto
                {
                    PromptTokens = usageElement.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                    CompletionTokens = usageElement.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0,
                    TotalTokens = usageElement.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0
                };
            }

            return new VideoAnalysisResultDto
            {
                RawContent = content,
                Events = events,
                Usage = usage
            };
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Qwen VL response: {Body}", responseBody);
            throw new UserFriendlyException($"Failed to parse video analysis result: {ex.Message}");
        }
    }

    private static string ExtractJson(string content)
    {
        var jsonStart = content.IndexOf("```json", StringComparison.Ordinal);
        if (jsonStart >= 0)
        {
            jsonStart = content.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = content.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return content[jsonStart..jsonEnd].Trim();
            }
        }

        jsonStart = content.IndexOf("```", StringComparison.Ordinal);
        if (jsonStart >= 0)
        {
            jsonStart = content.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = content.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return content[jsonStart..jsonEnd].Trim();
            }
        }

        var braceStart = content.IndexOf('{');
        var braceEnd = content.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            return content[braceStart..(braceEnd + 1)].Trim();
        }

        return content.Trim();
    }
}
