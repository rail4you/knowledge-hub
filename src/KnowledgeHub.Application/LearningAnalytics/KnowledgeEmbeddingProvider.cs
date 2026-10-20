using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI;
using KnowledgeHub.Application.Contracts.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>批量向量结果（Provider=none 表示不可用，调用方回退字符相似度）。</summary>
public record KnowledgeEmbeddingResult(string Provider, IReadOnlyList<float[]?> Vectors)
{
    public bool IsAvailable => !string.Equals(Provider, "none", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 知识点向量提供方，按优先级取可用实现：
/// 1) 已配置的 <c>EmbeddingService:BaseUrl</c>（项目既有向量服务，与检索共用）；
/// 2) 未配置时回退 DashScope OpenAI 兼容 <c>/embeddings</c>（复用 Qwen 动态 Key 与限流）；
/// 3) 都不可用 → Provider=none，归一化退回字符近似（Jaccard）。
/// </summary>
public interface IKnowledgeEmbeddingProvider
{
    Task<KnowledgeEmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    Task<float[]?> EmbedOneAsync(string text, CancellationToken cancellationToken = default);
}

public class KnowledgeEmbeddingProvider : IKnowledgeEmbeddingProvider, ITransientDependency
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IQwenCredentialProvider _credentialProvider;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KnowledgeEmbeddingProvider> _logger;

    private static DateTime _qwenDisabledUntil = DateTime.MinValue;

    public KnowledgeEmbeddingProvider(
        IEmbeddingService embeddingService,
        IQwenCredentialProvider credentialProvider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<KnowledgeEmbeddingProvider> logger)
    {
        _embeddingService = embeddingService;
        _credentialProvider = credentialProvider;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<KnowledgeEmbeddingResult> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return new KnowledgeEmbeddingResult("none", Array.Empty<float[]?>());
        }

        // 1) 项目既有向量服务
        try
        {
            if (_embeddingService.IsConfigured)
            {
                // 注意：既有实现返回的是「扁平化」向量（texts 的向量首尾相连），需要按维度切片
                var flat = await _embeddingService.GenerateBatchEmbeddingsAsync(texts.ToList());
                var list = new List<float[]?>();
                var dimension = flat.Count > 0 && flat.Count % texts.Count == 0 ? flat.Count / texts.Count : 0;
                for (var i = 0; i < texts.Count; i++)
                {
                    if (dimension <= 0)
                    {
                        list.Add(null);
                        continue;
                    }

                    var vector = new float[dimension];
                    flat.CopyTo(i * dimension, vector, 0, dimension);
                    list.Add(vector);
                }

                if (list.Any(v => v != null))
                {
                    return new KnowledgeEmbeddingResult("embedding-service", list);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "知识点向量：既有 Embedding 服务调用失败，尝试 Qwen 回退");
        }

        // 2) Qwen / DashScope 回退（失败后冷却 10 分钟，避免每个请求都重试）
        if (DateTime.UtcNow < _qwenDisabledUntil)
        {
            return new KnowledgeEmbeddingResult("none", new float[]?[texts.Count]);
        }

        try
        {
            // DashScope embeddings 单次批量上限 10，超出会被拒绝（batch size is invalid）
            var batchSize = _configuration.GetValue("LearningAnalytics:EmbeddingBatchSize", 10);
            if (batchSize <= 0)
            {
                batchSize = 10;
            }

            var all = new List<float[]?>();
            for (var offset = 0; offset < texts.Count; offset += batchSize)
            {
                var chunk = texts.Skip(offset).Take(batchSize).ToList();
                var vectors = await EmbedByQwenAsync(chunk, cancellationToken);
                if (vectors == null)
                {
                    return new KnowledgeEmbeddingResult("none", new float[]?[texts.Count]);
                }

                all.AddRange(vectors);
            }

            if (all.Any(v => v is { Length: > 0 }))
            {
                return new KnowledgeEmbeddingResult("qwen", all);
            }
        }
        catch (Exception ex)
        {
            _qwenDisabledUntil = DateTime.UtcNow.AddMinutes(10);
            _logger.LogWarning(ex, "知识点向量：Qwen embeddings 不可用，10 分钟内回退字符近似");
        }

        return new KnowledgeEmbeddingResult("none", new float[]?[texts.Count]);
    }

    public async Task<float[]?> EmbedOneAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await EmbedAsync(new[] { text }, cancellationToken);
        return result.Vectors.FirstOrDefault();
    }

    private async Task<List<float[]?>?> EmbedByQwenAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var apiKey = await _credentialProvider.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var baseUrl = _configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["LearningAnalytics:EmbeddingModel"] ?? "text-embedding-v4";
        var dimension = _configuration.GetValue("LearningAnalytics:EmbeddingDimension", 1024);

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["input"] = texts.ToList(),
        };
        if (dimension > 0)
        {
            body["dimensions"] = dimension;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var lease = await QwenClient.AcquireAsync(_configuration, cancellationToken);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_configuration.GetValue("LearningAnalytics:EmbeddingTimeoutSeconds", 60));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_configuration.GetValue("LearningAnalytics:EmbeddingTimeoutSeconds", 60)));

        var response = await client.SendAsync(request, cts.Token);
        var payload = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Qwen embeddings 返回 {Status}: {Body}", (int)response.StatusCode, Truncate(payload, 300));
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new float[]?[texts.Count];
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var index = item.TryGetProperty("index", out var idx) && idx.ValueKind == JsonValueKind.Number
                ? idx.GetInt32()
                : -1;
            if (index < 0 || index >= result.Length)
            {
                index = Array.FindIndex(result, v => v == null);
                if (index < 0)
                {
                    continue;
                }
            }

            var vector = new float[embedding.GetArrayLength()];
            var i = 0;
            foreach (var value in embedding.EnumerateArray())
            {
                vector[i++] = (float)value.GetDouble();
            }

            result[index] = vector;
        }

        return result.ToList();
    }

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max];
}
