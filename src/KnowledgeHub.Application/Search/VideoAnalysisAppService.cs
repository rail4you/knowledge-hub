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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace KnowledgeHub.Application.Search;

public class VideoAnalysisAppService : KnowledgeHubAppService, IVideoAnalysisAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoAnalysisAppService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string DefaultModel = "qwen3-vl-plus";
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
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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
        return await CallQwenVlApiAsync(videoDataUrl, prompt);
    }

    public async Task<VideoAnalysisResultDto> AnalyzeLocalVideoAsync(string filePath)
    {
        return await AnalyzeVideoTimelineAsync(new VideoAnalysisRequestDto { FilePath = filePath });
    }

    public async Task SaveVideoTimelineToMeiliSearchAsync(Guid videoId, string videoName, string videoUrl, VideoAnalysisResultDto analysisResult)
    {
        await EnsureVideosIndexExistsAsync();

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
            return;
        }

        _logger.LogInformation("Creating videos index in Meilisearch");

        var createContent = new { uid = VideosIndexName, primaryKey = "id" };
        var createResponse = await client.PostAsJsonAsync("/indexes", createContent);
        createResponse.EnsureSuccessStatusCode();

        var indexBase = client.BaseAddress + $"/indexes/{VideosIndexName}";

        await client.PostAsJsonAsync($"{indexBase}/settings/filterable-attributes",
            new[] { "videoId", "videoName", "indexedAt" });

        await client.PostAsJsonAsync($"{indexBase}/settings/searchable-attributes",
            new[] { "videoName", "eventDescription" });

        await client.PostAsJsonAsync($"{indexBase}/settings/sortable-attributes",
            new[] { "order", "indexedAt", "startTime" });

        _logger.LogInformation("Videos index created successfully");
    }

    private async Task<string> PrepareLocalVideoAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new UserFriendlyException($"Video file not found: {filePath}");
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

    private async Task<string> ConvertToMp4Async(string inputPath)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"video_{Guid.NewGuid()}.mp4");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" -vcodec libx264 -crf 28 -preset fast -y \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new UserFriendlyException("Cannot start ffmpeg. Please install ffmpeg first.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new UserFriendlyException($"Video conversion failed: {error}");
        }

        return outputPath;
    }

    private async Task<string> CompressVideoAsync(string inputPath)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"video_compressed_{Guid.NewGuid()}.mp4");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" -vcodec libx264 -crf 32 -preset slow -vf \"scale=-2:480\" -y \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new UserFriendlyException("Cannot start ffmpeg.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            _logger.LogWarning("Video compression failed: {Error}, using original", error);
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
                            fps = 1
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
