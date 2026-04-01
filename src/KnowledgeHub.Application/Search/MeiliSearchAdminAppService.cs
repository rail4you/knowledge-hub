using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.Extensions.Options;

namespace KnowledgeHub.Application.Search;

public class MeiliSearchAdminAppService : KnowledgeHubAppService, IMeiliSearchAdminAppService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<MeilisearchOptions> _options;

    public MeiliSearchAdminAppService(IOptions<MeilisearchOptions> options, HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.Value.Host);
        if (!string.IsNullOrEmpty(_options.Value.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.Value.ApiKey}");
        }
    }

    public async Task<MeiliDashboardDto> GetDashboardAsync()
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        var dashboard = new MeiliDashboardDto();

        try
        {
            var healthTask = GetAsync<MeiliHealthDto>("/health");
            var versionTask = GetAsync<MeiliVersionDto>("/version");
            var statsTask = GetAsync<MeiliStatsRawDto>("/stats");
            var indexesTask = GetAsync<List<MeiliIndexDto>>("/indexes");
            var tasksTask = GetAsync<MeiliTasksRawDto>("/tasks?limit=20");

            await Task.WhenAll(healthTask, versionTask, statsTask, indexesTask, tasksTask);

            dashboard.Health = healthTask.Result ?? new MeiliHealthDto();
            dashboard.Version = versionTask.Result ?? new MeiliVersionDto();
            dashboard.Stats = MapStats(statsTask.Result);
            dashboard.Indexes = indexesTask.Result ?? new List<MeiliIndexDto>();
            dashboard.RecentTasks = tasksTask.Result?.Results ?? new List<MeiliTaskDto>();

            // Get embedders for the primary index
            var indexName = _options.Value.IndexName;
            if (!string.IsNullOrEmpty(indexName))
            {
                try
                {
                    var embedders = await GetEmbeddersInternalAsync(indexName);
                    dashboard.Embedders = embedders;
                }
                catch
                {
                    dashboard.Embedders = new Dictionary<string, MeiliEmbedderDto>();
                }
            }
        }
        catch
        {
            dashboard.Health.Status = "unavailable";
        }

        return dashboard;
    }

    public async Task<MeiliIndexStatsDto> GetIndexStatsAsync(string indexUid)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        var result = await GetAsync<MeiliIndexStatsDto>($"/indexes/{indexUid}/stats");
        return result ?? new MeiliIndexStatsDto();
    }

    public async Task<Dictionary<string, MeiliEmbedderDto>> GetEmbeddersAsync(string indexUid)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        return await GetEmbeddersInternalAsync(indexUid);
    }

    public async Task<List<MeiliTaskDto>> GetRecentTasksAsync(int limit = 20)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        var result = await GetAsync<MeiliTasksRawDto>($"/tasks?limit={limit}");
        return result?.Results ?? new List<MeiliTaskDto>();
    }

    private async Task<Dictionary<string, MeiliEmbedderDto>> GetEmbeddersInternalAsync(string indexUid)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/indexes/{indexUid}/settings/embedders");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, MeiliEmbedderDto>();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var embedder = new MeiliEmbedderDto();
                var obj = prop.Value;

                if (obj.TryGetProperty("source", out var source))
                    embedder.Source = source.GetString() ?? "";
                if (obj.TryGetProperty("url", out var url))
                    embedder.Url = url.GetString();
                if (obj.TryGetProperty("model", out var model))
                    embedder.Model = model.GetString();
                if (obj.TryGetProperty("dimensions", out var dimensions))
                    embedder.Dimensions = dimensions.GetInt32();
                if (obj.TryGetProperty("documentTemplate", out var docTemplate))
                    embedder.DocumentTemplate = docTemplate.GetString();

                result[prop.Name] = embedder;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, MeiliEmbedderDto>();
        }
    }

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch
        {
            return null;
        }
    }

    private static MeiliStatsDto MapStats(MeiliStatsRawDto? raw)
    {
        if (raw == null) return new MeiliStatsDto();

        var stats = new MeiliStatsDto
        {
            DatabaseSize = raw.DatabaseSize,
            UsedDatabaseSize = raw.UsedDatabaseSize,
            LastUpdate = raw.LastUpdate,
        };

        if (raw.Indexes != null)
        {
            foreach (var kvp in raw.Indexes)
            {
                stats.Indexes[kvp.Key] = new MeiliIndexStatsDto
                {
                    NumberOfDocuments = kvp.Value.NumberOfDocuments,
                    IsIndexing = kvp.Value.IsIndexing,
                    FieldDistribution = kvp.Value.FieldDistribution ?? new Dictionary<string, long>()
                };
            }
        }

        return stats;
    }
}

internal class MeiliStatsRawDto
{
    public long DatabaseSize { get; set; }
    public long UsedDatabaseSize { get; set; }
    public DateTime? LastUpdate { get; set; }
    public Dictionary<string, MeiliIndexStatsRawDto>? Indexes { get; set; }
}

internal class MeiliIndexStatsRawDto
{
    public long NumberOfDocuments { get; set; }
    public bool IsIndexing { get; set; }
    public Dictionary<string, long>? FieldDistribution { get; set; }
}

internal class MeiliTasksRawDto
{
    public List<MeiliTaskDto>? Results { get; set; }
}
