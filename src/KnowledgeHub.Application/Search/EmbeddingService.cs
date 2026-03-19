using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KnowledgeHub.Application.Search;

public class EmbeddingService : IEmbeddingService
{
    private readonly IOptions<EmbeddingServiceOptions> _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;

    public bool IsConfigured => !string.IsNullOrEmpty(_options.Value.BaseUrl);

    public EmbeddingService(
        IOptions<EmbeddingServiceOptions> options,
        HttpClient httpClient,
        ILogger<EmbeddingService> logger)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;

        if (IsConfigured)
        {
            _httpClient.BaseAddress = new Uri(_options.Value.BaseUrl);
            if (!string.IsNullOrEmpty(_options.Value.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.Value.ApiKey}");
            }
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Embedding service is not configured. Returning zero vector.");
            return new float[768];
        }

        try
        {
            var request = new { text };
            var response = await _httpClient.PostAsJsonAsync("", request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
            
            if (result?.Embedding != null && result.Embedding.Count > 0)
            {
                return result.Embedding.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
        }

        return new float[768];
    }

    public async Task<List<float>> GenerateBatchEmbeddingsAsync(List<string> texts)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Embedding service is not configured. Returning zero vectors.");
            var zeroVector = new float[768];
            var result = new List<float>();
            foreach (var _ in texts)
            {
                result.AddRange(zeroVector);
            }
            return result;
        }

        try
        {
            var request = new { texts };
            var response = await _httpClient.PostAsJsonAsync("/batch", request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<BatchEmbeddingResponse>();
            
            if (result?.Embeddings != null)
            {
                var flatList = new List<float>();
                foreach (var embedding in result.Embeddings)
                {
                    flatList.AddRange(embedding);
                }
                return flatList;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating batch embeddings");
        }

        var zeroVectors = new float[768 * texts.Count];
        return new List<float>(zeroVectors);
    }
}

public class EmbeddingServiceOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int Dimension { get; set; } = 768;
}

internal class EmbeddingResponse
{
    public List<float>? Embedding { get; set; }
}

internal class BatchEmbeddingResponse
{
    public List<List<float>>? Embeddings { get; set; }
}
