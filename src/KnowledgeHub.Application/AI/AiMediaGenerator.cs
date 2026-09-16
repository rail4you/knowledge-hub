using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Resources.FileStorage;
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

    /// <summary>
    /// 提交图生视频前的内容预检：用 Qwen VL 检查首帧图片、用 Qwen 检查提示词，
    /// 疑似含违规内容时直接抛中文友好异常，避免提交后因平台内容安全拦截而失败。
    /// 检测服务本身异常时按"未检出"放行（提交阶段的内容安全拦截仍兜底），绝不阻塞正常生成。
    /// </summary>
    Task CheckVideoInputAsync(string imageUrl, string prompt);

    Task<MediaGenerationTaskDto> GetTaskAsync(string taskId);
    Task<MediaGenerationTaskDto> WaitForCompletionAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default, Func<int, string?, Task>? onProgressAsync = null);

    /// <summary>把本地持久化的首帧图片（/uploads/...）转成 base64 data URL 供通义万相图生视频使用。</summary>
    Task<string> ResolveImageUrlForI2vAsync(string imageUrl);
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
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AiMediaGenerator> _logger;

    public AiMediaGenerator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IQwenCredentialProvider qwenCredentials,
        IFileStorageService fileStorage,
        ILogger<AiMediaGenerator> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _qwenCredentials = qwenCredentials;
        _fileStorage = fileStorage;
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

    public async Task CheckVideoInputAsync(string imageUrl, string prompt)
    {
        if (!(_configuration.GetValue("Qwen:VideoContentPreCheck", true)))
        {
            return;
        }

        await CheckImageContentAsync(imageUrl);
        await CheckPromptContentAsync(prompt);
    }

    /// <summary>用 Qwen VL 判断首帧图片是否含不适当内容（尽力而为，服务异常静默放行）。</summary>
    private async Task CheckImageContentAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        try
        {
            // 本地持久化 / 上传的图片转 base64 data URL（与图生视频提交保持一致）；公网 URL 原样使用
            var image = await ResolveImageUrlForI2vAsync(imageUrl);
            var apiKey = await _qwenCredentials.GetApiKeyAsync();
            var baseUrl = (_configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1").TrimEnd('/');
            var model = _configuration["Qwen:VisionModel"] ?? "qwen3-vl-flash";

            var payload = JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "image_url", image_url = new { url = image } },
                            new { type = "text", text = ImageModerationPrompt },
                        },
                    },
                },
                temperature = 0.01,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Qwen VL 图片内容预检失败 {Status}: {Body}", (int)response.StatusCode, text);
                return;
            }

            using var doc = JsonDocument.Parse(text);
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            if (IsModerationUnsafe(content))
            {
                throw new UserFriendlyException(
                    "检测到图片可能包含不适当内容，无法用于视频生成。请更换一张更合适的教学场景图片后重试。");
            }
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 预检服务不可用不能阻塞生成；提交阶段的内容安全拦截仍会兜底并给出中文提示
            _logger.LogWarning(ex, "Qwen VL 图片内容预检异常，跳过预检");
        }
    }

    /// <summary>用 Qwen 判断运镜/动作提示词是否可能触发内容安全拦截（尽力而为）。</summary>
    private async Task CheckPromptContentAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        try
        {
            var apiKey = await _qwenCredentials.GetApiKeyAsync();
            var baseUrl = (_configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1").TrimEnd('/');
            var model = _configuration["Qwen:Model"] ?? "qwen-flash";

            var payload = JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"{TextModerationPrompt}\n\n待检测提示词：\n{prompt}",
                    },
                },
                temperature = 0.01,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Qwen 提示词内容预检失败 {Status}: {Body}", (int)response.StatusCode, text);
                return;
            }

            using var doc = JsonDocument.Parse(text);
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            if (IsModerationUnsafe(content))
            {
                throw new UserFriendlyException(
                    "检测到提示词可能包含不适当内容，无法用于视频生成。请修改运镜/动作提示词后重试。");
            }
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qwen 提示词内容预检异常，跳过预检");
        }
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
            var friendly = ToModerationFriendlyMessage(text);
            throw new UserFriendlyException(friendly ?? $"查询生成任务失败：{ExtractErrorMessage(text)}");
        }

        using var doc = JsonDocument.Parse(text);
        return ReadTaskStatus(taskId, doc);
    }

    /// <summary>
    /// 本地持久化的首帧图片（/uploads/... 或 App:SelfUrl 开头的地址）DashScope 无法访问（如 localhost），
    /// 转成 base64 data URL 再提交；公网 URL（OSS / 通义万相临时地址）原样返回。
    /// </summary>
    public async Task<string> ResolveImageUrlForI2vAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        var localPath = TryResolveLocalPath(imageUrl);
        if (localPath == null)
        {
            return imageUrl;
        }

        try
        {
            await using var stream = await _fileStorage.GetAsync(localPath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var ext = Path.GetExtension(localPath).ToLowerInvariant();
            var mime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream",
            };

            return $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "本地首帧图片转 base64 失败，回退原 URL：{Url}", imageUrl);
            return imageUrl;
        }
    }

    private string? TryResolveLocalPath(string url)
    {
        var selfUrl = (_configuration["App:SelfUrl"] ?? string.Empty).TrimEnd('/');
        if (!string.IsNullOrEmpty(selfUrl) && url.StartsWith(selfUrl, StringComparison.OrdinalIgnoreCase))
        {
            url = url[selfUrl.Length..];
        }

        const string uploadsPrefix = "/uploads/";
        return url.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase)
            ? url[uploadsPrefix.Length..]
            : null;
    }

    public async Task<MediaGenerationTaskDto> WaitForCompletionAsync(
        string taskId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        Func<int, string?, Task>? onProgressAsync = null)
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

            if (onProgressAsync != null)
            {
                var remaining = deadline - DateTime.UtcNow;
                var pct = 10 + (int)((1 - remaining.TotalSeconds / timeout.TotalSeconds) * 80);
                await onProgressAsync(Math.Clamp(pct, 10, 89), null);
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
            // 内容安全拦截（inappropriate content / DataInspectionFailed 等）直接给中文说明，不把英文细节抛给用户
            var friendly = ToModerationFriendlyMessage(text);
            throw new UserFriendlyException(friendly ?? $"提交生成任务失败：{ExtractErrorMessage(text)}");
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
            var raw = GetString(output, "message");
            dto.Error = ToModerationFriendlyMessage(raw) ?? (raw ?? "生成失败");
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

    private const string ImageModerationPrompt =
        "你是图片内容安全审核员。请判断这张图片是否包含以下不适合用于教育视频生成的内容：" +
        "色情或裸露、暴力或血腥、恐怖惊悚、政治敏感、违禁物品、仇恨歧视、低俗恶搞、广告推广。\n" +
        "如果包含以上任一类内容，只输出：{\"safe\":false}；否则只输出：{\"safe\":true}。不要输出任何其他文字。";

    private const string TextModerationPrompt =
        "你是文本内容安全审核员。请判断以下提示词是否包含不适合用于教育视频生成的内容：" +
        "色情或裸露、暴力或血腥、恐怖惊悚、政治敏感、违禁物品、仇恨歧视、低俗恶搞、广告推广。\n" +
        "如果包含以上任一类内容，只输出：{\"safe\":false}；否则只输出：{\"safe\":true}。不要输出任何其他文字。";

    /// <summary>
    /// 内容安全拦截的英文/原始错误（Input data may contain inappropriate content、DataInspectionFailed 等）
    /// 统一转成面向用户的中文说明；非内容拦截错误返回 null，由调用方按原文处理。
    /// </summary>
    private static string? ToModerationFriendlyMessage(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return null;
        }

        var m = rawMessage;
        if (m.Contains("inappropriate content", StringComparison.OrdinalIgnoreCase)
            || m.Contains("datainspection", StringComparison.OrdinalIgnoreCase)
            || m.Contains("data_inspection", StringComparison.OrdinalIgnoreCase)
            || m.Contains("content_filter", StringComparison.OrdinalIgnoreCase)
            || m.Contains("content filter", StringComparison.OrdinalIgnoreCase)
            || m.Contains("安全策略拦截", StringComparison.OrdinalIgnoreCase)
            || m.Contains("内容安全", StringComparison.OrdinalIgnoreCase)
            || m.Contains("不适当", StringComparison.OrdinalIgnoreCase)
            || m.Contains("敏感内容", StringComparison.OrdinalIgnoreCase))
        {
            return "生成被平台安全策略拦截：图片或提示词包含不适当内容，无法生成。" +
                   "请更换一张更合适的图片，并检查提示词中是否包含敏感或违规表述后重试。";
        }

        return null;
    }

    /// <summary>
    /// 解析审核模型结论：仅当明确输出 {"safe":false} 或安全对齐导致拒绝作答时判定为不安全，
    /// 减少误拦截（拿不准一律视为安全，交给提交阶段的内容安全拦截兜底）。
    /// </summary>
    private static bool IsModerationUnsafe(string content)
    {
        var idx = content.IndexOf("\"safe\"", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var tail = content.Substring(idx, Math.Min(24, content.Length - idx));
            // 注意先判 false："false" 包含子串 "true"
            if (tail.Contains("false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (tail.Contains("true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // 模型拒绝回答 = 触发了安全对齐，同样视为内容不合规
        return content.Contains("拒绝", StringComparison.OrdinalIgnoreCase)
            || content.Contains("无法回答", StringComparison.OrdinalIgnoreCase)
            || content.Contains("无法提供", StringComparison.OrdinalIgnoreCase)
            || content.Contains("不能提供", StringComparison.OrdinalIgnoreCase);
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
