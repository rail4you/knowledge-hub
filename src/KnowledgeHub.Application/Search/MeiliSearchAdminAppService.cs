using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

public class MeiliSearchAdminAppService : KnowledgeHubAppService, IMeiliSearchAdminAppService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<MeilisearchOptions> _options;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _versionRepository;
    private readonly ICurrentTenant _currentTenant;

    public MeiliSearchAdminAppService(
        IOptions<MeilisearchOptions> options,
        HttpClient httpClient,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> versionRepository,
        ICurrentTenant currentTenant)
    {
        _options = options;
        _httpClient = httpClient;
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _currentTenant = currentTenant;
        _httpClient.BaseAddress = new Uri(_options.Value.Host);
        if (!string.IsNullOrEmpty(_options.Value.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.Value.ApiKey}");
        }
    }

    public async Task<MeiliDashboardDto> GetDashboardAsync(Guid? tenantId = null)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        // 如果当前用户已登录且属于某个租户，则强制使用该租户ID
        var currentTenantId = _currentTenant.Id;
        if (currentTenantId.HasValue)
        {
            tenantId = currentTenantId;
        }

        var dashboard = new MeiliDashboardDto();

        try
        {
            var healthTask = GetAsync<MeiliHealthDto>("/health");
            var versionTask = GetAsync<MeiliVersionDto>("/version");
            var statsTask = GetAsync<MeiliStatsRawDto>("/stats");
            var indexesTask = GetAsync<MeiliIndexesRawDto>("/indexes");
            var tasksTask = GetAsync<MeiliTasksRawDto>("/tasks?limit=20");

            await Task.WhenAll(healthTask, versionTask, statsTask, indexesTask, tasksTask);

            dashboard.Health = healthTask.Result ?? new MeiliHealthDto();
            dashboard.Version = versionTask.Result ?? new MeiliVersionDto();
            dashboard.Stats = MapStats(statsTask.Result);
            dashboard.Indexes = indexesTask.Result?.Results ?? new List<MeiliIndexDto>();
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

    public async Task<List<MeiliDocumentGroupDto>> GetIndexDocumentsAsync(string indexUid, int limit = 200, Guid? tenantId = null)
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        // 如果当前用户已登录且属于某个租户，则强制使用该租户ID
        var currentTenantId = _currentTenant.Id;
        if (currentTenantId.HasValue)
        {
            tenantId = currentTenantId;
        }

        try
        {
            // 构建 tenantId filter
            var tenantFilter = tenantId.HasValue ? $"tenantId = \"{tenantId}\"" : "";

            // 构建查询参数
            var queryParams = new List<string> { $"limit={limit}" };
            if (!string.IsNullOrEmpty(tenantFilter))
            {
                queryParams.Add($"filter={Uri.EscapeDataString(tenantFilter)}");
            }

            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"/indexes/{indexUid}/documents?{queryString}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");

            var groups = new Dictionary<string, MeiliDocumentGroupDto>();

            foreach (var item in results.EnumerateArray())
            {
                var resourceName = item.TryGetProperty("resourceName", out var rnProp)
                    ? rnProp.GetString() ?? "unknown"
                    : item.TryGetProperty("videoName", out var vnProp)
                        ? vnProp.GetString() ?? "unknown"
                        : "unknown";
                var resourceId = item.TryGetProperty("resourceId", out var riProp)
                    ? riProp.GetString()
                    : item.TryGetProperty("videoId", out var viProp)
                        ? viProp.GetString()
                        : null;
                var fileExtension = item.TryGetProperty("fileExtension", out var feProp)
                    ? feProp.GetString() : null;
                var pageNumber = item.TryGetProperty("pageNumber", out var pnProp)
                    ? pnProp.GetInt32() : 0;
                var pageTitle = item.TryGetProperty("pageTitle", out var ptProp)
                    ? ptProp.GetString() : null;
                var id = item.TryGetProperty("id", out var idProp)
                    ? idProp.GetString() ?? "" : "";

                // Detect resource type: video if it has startTime or eventDescription
                var isVideo = item.TryGetProperty("startTime", out _) ||
                              item.TryGetProperty("eventDescription", out _);
                var resourceType = isVideo ? "video" : "document";

                // Read video fields
                var startTime = item.TryGetProperty("startTime", out var stProp)
                    ? stProp.GetString() : null;
                var endTime = item.TryGetProperty("endTime", out var etProp)
                    ? etProp.GetString() : null;
                var eventDescription = item.TryGetProperty("eventDescription", out var edProp)
                    ? edProp.GetString() : null;
                var videoUrl = item.TryGetProperty("videoUrl", out var vuProp)
                    ? vuProp.GetString() : null;

                var pageContent = item.TryGetProperty("pageContent", out var pcProp)
                    ? pcProp.GetString() : null;
                var uploadDate = item.TryGetProperty("uploadDate", out var udProp)
                    ? udProp.GetString()
                    : item.TryGetProperty("indexedAt", out var iaProp)
                        ? iaProp.GetString() : null;

                if (!groups.TryGetValue(resourceName, out var group))
                {
                    group = new MeiliDocumentGroupDto
                    {
                        ResourceName = resourceName,
                        ResourceId = resourceId,
                        FileExtension = fileExtension,
                        ResourceType = resourceType,
                        VideoUrl = videoUrl,
                        UploadDate = uploadDate
                    };
                    groups[resourceName] = group;
                }

                group.Pages.Add(new MeiliDocumentPageDto
                {
                    Id = id,
                    PageNumber = pageNumber,
                    PageTitle = pageTitle,
                    PageContent = pageContent,
                    StartTime = startTime,
                    EndTime = endTime,
                    EventDescription = eventDescription
                });
                group.PageCount = group.Pages.Count;
            }

            return groups.Values
                .OrderByDescending(g => g.UploadDate ?? string.Empty)
                .ThenByDescending(g => g.PageCount)
                .ToList();
        }
        catch
        {
            return new List<MeiliDocumentGroupDto>();
        }
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

    public async Task<List<MeiliIndexDto>> GetIndexesAsync()
    {
        await CheckPolicyAsync(KnowledgeHubPermissions.Search.ViewStatistics);

        var result = await GetAsync<MeiliIndexesRawDto>("/indexes");
        return result?.Results ?? new List<MeiliIndexDto>();
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

internal class MeiliIndexesRawDto
{
    public List<MeiliIndexDto>? Results { get; set; }
}
