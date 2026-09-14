using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// 通义万相媒体生成核心：提交任务 / 查询状态 / 等待完成。
/// 与 HTTP 层（配额、用量、鉴权）解耦，便于后台任务复用。
/// </summary>
public interface IAiMediaGenerator
{
    string ImageModel { get; }
    string VideoModel { get; }

    Task<MediaGenerationTaskDto> SubmitImageAsync(string prompt, string? size, string? negativePrompt);
    Task<MediaGenerationTaskDto> SubmitVideoAsync(string imageUrl, string prompt, int duration);
    Task<MediaGenerationTaskDto> GetTaskAsync(string taskId);
    Task<MediaGenerationTaskDto> WaitForCompletionAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public class AiMediaGenerator : IAiMediaGenerator, ITransientDependency
{
    private const string DefaultNativeBaseUrl = "https://dashscope.aliyuncs.com/api/v1";
    private const string DefaultImageModel = "wan2.2-t2i-flash";
    private const string DefaultVideoModel = "wan2.2-i2v-flash";
    private const string DefaultImageSize = "1024*1024";
    private const int MaxVideoDurationSeconds = 5;
    private const int PollIntervalMs = 4000;

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IQwenCredentialProvider _qwenCredentials;
    private readonly ILogger<AiMediaGenerator> _logger;

    public AiMediaGenerator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IQwenCredentialProvider qwenCredentials,
        ILogger<AiMediaGenerator> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _qwenCredentials = qwenCredentials;
        _logger = logger;
    }

    public string ImageModel => _configuration["Qwen:ImageModel"] ?? DefaultImageModel;

    public string VideoModel => _configuration["Qwen:VideoModel"] ?? DefaultVideoModel;

    public async Task<MediaGenerationTaskDto> SubmitImageAsync(string prompt, string? size, string? negativePrompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new UserFriendlyException("请输入图片提示词");
        }

        var imageInput = new Dictionary<string, object?> { ["prompt"] = prompt.Trim() };
        if (!string.IsNullOrWhiteSpace(negativePrompt))
        {
            imageInput["negative_prompt"] = negativePrompt.Trim();
        }

        var body = new Dictionary<string, object?>
        {
            ["model"] = ImageModel,
            ["input"] = imageInput,
            ["parameters"] = new Dictionary<string, object?>
            {
                ["size"] = string.IsNullOrWhiteSpace(size) ? DefaultImageSize : size!.Trim(),
                ["n"] = 1,
            },
        };

        var doc = await PostAsync("/services/aigc/text2image/image-synthesis", body);
        return ReadCreatedTask(doc);
    }

    public async Task<MediaGenerationTaskDto> SubmitVideoAsync(string imageUrl, string prompt, int duration)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new UserFriendlyException("请先生成或选择首帧图片");
        }
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new UserFriendlyException("请输入视频提示词");
        }

        var safeDuration = duration <= 0 ? MaxVideoDurationSeconds : Math.Min(duration, MaxVideoDurationSeconds);
        var body = new Dictionary<string, object?>
        {
            ["model"] = VideoModel,
            ["input"] = new Dictionary<string, object?>
            {
                ["prompt"] = prompt.Trim(),
                ["img_url"] = imageUrl.Trim(),
            },
            ["parameters"] = new Dictionary<string, object?>
            {
                ["resolution"] = "480P",
                ["duration"] = safeDuration,
            },
        };

        var doc = await PostAsync("/services/aigc/video-generation/video-synthesis", body);
        return ReadCreatedTask(doc);
    }

    public async Task<MediaGenerationTaskDto> GetTaskAsync(string taskId)
    {
        if (!Guid.TryParse(taskId, out _))
        {
            throw new UserFriendlyException("无效的任务标识");
        }

        var apiKey = await _qwenCredentials.GetApiKeyAsync();
        var url = $"{NativeBaseUrl}/tasks/{taskId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new UserFriendlyException($"查询生成任务失败：{ExtractErrorMessage(text)}");
        }

        using var doc = JsonDocument.Parse(text);
        return ReadTaskStatus(taskId, doc);
    }

    public async Task<MediaGenerationTaskDto> WaitForCompletionAsync(
        string taskId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var task = await GetTaskAsync(taskId);
            var status = (task.Status ?? string.Empty).ToUpperInvariant();
            if (status is "SUCCEEDED" or "FAILED" or "CANCELED" or "UNKNOWN")
            {
                return task;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new UserFriendlyException("生成超时，请稍后重试");
            }

            await Task.Delay(PollIntervalMs, cancellationToken);
        }
    }

    private async Task<JsonDocument> PostAsync(string path, object body)
    {
        var apiKey = await _qwenCredentials.GetApiKeyAsync();
        var payload = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{NativeBaseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("X-DashScope-Async", "enable");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("DashScope 媒体生成提交失败 {Status}: {Body}", (int)response.StatusCode, text);
            throw new UserFriendlyException($"提交生成任务失败：{ExtractErrorMessage(text)}");
        }

        return JsonDocument.Parse(text);
    }

    private string NativeBaseUrl =>
        (_configuration["Qwen:NativeBaseUrl"] ?? DefaultNativeBaseUrl).TrimEnd('/');

    private static MediaGenerationTaskDto ReadCreatedTask(JsonDocument doc)
    {
        var output = doc.RootElement.GetProperty("output");
        return new MediaGenerationTaskDto
        {
            TaskId = GetString(output, "task_id") ?? string.Empty,
            Status = GetString(output, "task_status") ?? "PENDING",
        };
    }

    private static MediaGenerationTaskDto ReadTaskStatus(string taskId, JsonDocument doc)
    {
        var dto = new MediaGenerationTaskDto { TaskId = taskId };
        if (!doc.RootElement.TryGetProperty("output", out var output))
        {
            dto.Status = "UNKNOWN";
            dto.Error = "任务返回格式异常";
            return dto;
        }

        dto.Status = GetString(output, "task_status") ?? "UNKNOWN";

        if (string.Equals(dto.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            dto.VideoUrl = GetString(output, "video_url");
            if (output.TryGetProperty("results", out var results)
                && results.ValueKind == JsonValueKind.Array
                && results.GetArrayLength() > 0)
            {
                dto.ImageUrl = GetString(results[0], "url");
            }
        }
        else if (string.Equals(dto.Status, "FAILED", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(dto.Status, "CANCELED", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(dto.Status, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            dto.Error = GetString(output, "message") ?? "生成失败";
        }

        return dto;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
        return null;
    }

    private static string ExtractErrorMessage(string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            if (doc.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? responseText;
            }
            if (doc.RootElement.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String)
            {
                return code.GetString() ?? responseText;
            }
        }
        catch
        {
            // 非 JSON 响应，直接返回原文
        }

        return responseText.Length > 500 ? responseText[..500] : responseText;
    }
}
