using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources;

public class MeiliFormatted
{
    [System.Text.Json.Serialization.JsonPropertyName("pageContent")]
    public string? pageContent { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("pageTitle")]
    public string? pageTitle { get; set; }

    // Video-specific highlighted fields
    [System.Text.Json.Serialization.JsonPropertyName("eventDescription")]
    public string? eventDescription { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("videoName")]
    public string? videoName { get; set; }
}

public class DocumentPage
{
    public string Id { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("resourceName")]
    public string? ResourceName { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("pageContent")]
    public string? pageContent { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("pageTitle")]
    public string? pageTitle { get; set; }
    public MeiliFormatted? _formatted { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("fileExtension")]
    public string FileExtension { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("resourceType")]
    public int ResourceType { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("uploadDate")]
    public DateTime UploadDate { get; set; }
    public double RankingScore { get; set; }

    // Video-specific fields
    [System.Text.Json.Serialization.JsonPropertyName("videoId")]
    public string? VideoId { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("videoName")]
    public string? VideoName { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("videoUrl")]
    public string? VideoUrl { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("startTime")]
    public string? StartTime { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("endTime")]
    public string? EndTime { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("eventDescription")]
    public string? EventDescription { get; set; }
}

public interface ISearchService
{
    Task InitializeAsync(string? indexName = null);
    Task AddDocumentsAsync(IEnumerable<DocumentPage> documents, string? indexName = null);
    Task<SearchResult> SearchAsync(string query, int limit = 20, int offset = 0, string? indexName = null);
    Task<SearchResult> SemanticSearchAsync(string query, int limit = 20, int offset = 0, string? indexName = null);
    Task DeleteDocumentAsync(string id, string? indexName = null);
    Task DeleteByResourceIdAsync(string resourceId, string? indexName = null);
}

public class SearchResult
{
    public List<DocumentPage> Hits { get; set; } = new();
    public int TotalHits { get; set; }
    public double ProcessingTimeMs { get; set; }
}

public class MeiliSearchService : ISearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _defaultIndexName = "movie";
    private bool _initialized = false;
    private string? _initializedIndexName;

    public MeiliSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Support both Docker-style (Meilisearch__Host) and direct env var naming
        var host = Environment.GetEnvironmentVariable("MEILISEARCH_HOST")
            ?? Environment.GetEnvironmentVariable("Meilisearch__Host")
            ?? "http://localhost:7700";
        var apiKey = Environment.GetEnvironmentVariable("MEILISEARCH_API_KEY")
            ?? Environment.GetEnvironmentVariable("Meilisearch__ApiKey")
            ?? "aSampleMasterKey";
        _httpClient.BaseAddress = new Uri(host);
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    public async Task InitializeAsync(string? indexName = null)
    {
        var effectiveIndexName = indexName ?? _defaultIndexName;
        if (_initialized && _initializedIndexName == effectiveIndexName) return;

        try
        {
            var response = await _httpClient.GetAsync($"/indexes/{effectiveIndexName}");
            if (!response.IsSuccessStatusCode)
            {
                var createContent = new { uid = effectiveIndexName, primaryKey = "id" };
                await _httpClient.PostAsJsonAsync("/indexes", createContent);
            }

            var indexPath = $"/indexes/{effectiveIndexName}";
            
            await _httpClient.PostAsJsonAsync($"{indexPath}/settings/filterable-attributes", 
                new[] { "resourceId", "resourceType", "categoryName", "fileExtension" });
            
            await _httpClient.PostAsJsonAsync($"{indexPath}/settings/searchable-attributes", 
                new[] { "pageContent", "pageTitle", "resourceName" });
            
            await _httpClient.PostAsJsonAsync($"{indexPath}/settings/sortable-attributes", 
                new[] { "uploadDate", "pageNumber", "resourceName" });

            _initialized = true;
            _initializedIndexName = effectiveIndexName;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Meilisearch: {ex.Message}");
        }
    }

    public async Task AddDocumentsAsync(IEnumerable<DocumentPage> documents, string? indexName = null)
    {
        await InitializeAsync(indexName);
        var effectiveIndexName = indexName ?? _defaultIndexName;
        
        var json = JsonSerializer.Serialize(documents);
        await _httpClient.PostAsync(
            $"/indexes/{effectiveIndexName}/documents",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public async Task<SearchResult> SearchAsync(string query, int limit = 20, int offset = 0, string? indexName = null)
    {
        var effectiveIndexName = indexName ?? _defaultIndexName;
        await InitializeAsync(effectiveIndexName);
        
        var searchParams = new
        {
            q = query,
            limit,
            offset,
            attributesToHighlight = new[] { "pageContent", "pageTitle", "eventDescription", "videoName" },
            highlightPreTag = "<mark>",
            highlightPostTag = "</mark>",
            showRankingScore = true
        };

        var response = await _httpClient.PostAsJsonAsync($"/indexes/{effectiveIndexName}/search", searchParams);
        var result = await response.Content.ReadFromJsonAsync<MeiliSearchResponse>();

        return new SearchResult
        {
            Hits = result?.Hits?.Select(h => new DocumentPage
            {
                Id = h.Id,
                ResourceId = h.ResourceId,
                ResourceName = h.ResourceName ?? string.Empty,
                PageNumber = h.PageNumber,
                pageContent = h._formatted?.pageContent ?? h.pageContent,
                pageTitle = h._formatted?.pageTitle ?? h.pageTitle,
                FileExtension = h.FileExtension ?? string.Empty,
                ResourceType = h.ResourceType,
                CategoryName = h.CategoryName,
                UploadDate = h.UploadDate,
                RankingScore = h.RankingScore,

                // Video-specific fields
                VideoId = h.VideoId,
                VideoName = h.VideoName,
                VideoUrl = h.VideoUrl,
                StartTime = h.StartTime,
                EndTime = h.EndTime,
                EventDescription = h._formatted?.eventDescription ?? h.EventDescription
            }).ToList() ?? new List<DocumentPage>(),
            TotalHits = result?.EstimatedTotalHits ?? 0,
            ProcessingTimeMs = result?.ProcessingTimeMs ?? 0
        };
    }

    public async Task<SearchResult> SemanticSearchAsync(string query, int limit = 20, int offset = 0, string? indexName = null)
    {
        var effectiveIndexName = indexName ?? _defaultIndexName;
        await InitializeAsync(effectiveIndexName);

        var searchParams = new
        {
            q = query,
            limit,
            offset,
            attributesToHighlight = new[] { "pageContent", "pageTitle", "eventDescription", "videoName" },
            highlightPreTag = "<mark>",
            highlightPostTag = "</mark>",
            showRankingScore = true,
            hybrid = new
            {
                embedder = "qwen",
                semanticRatio = 1.0
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"/indexes/{effectiveIndexName}/search", searchParams);
        var result = await response.Content.ReadFromJsonAsync<MeiliSearchResponse>();

        return new SearchResult
        {
            Hits = result?.Hits?.Select(h => new DocumentPage
            {
                Id = h.Id,
                ResourceId = h.ResourceId,
                ResourceName = h.ResourceName ?? string.Empty,
                PageNumber = h.PageNumber,
                pageContent = h._formatted?.pageContent ?? h.pageContent,
                pageTitle = h._formatted?.pageTitle ?? h.pageTitle,
                FileExtension = h.FileExtension ?? string.Empty,
                ResourceType = h.ResourceType,
                CategoryName = h.CategoryName,
                UploadDate = h.UploadDate,
                RankingScore = h.RankingScore,

                // Video-specific fields
                VideoId = h.VideoId,
                VideoName = h.VideoName,
                VideoUrl = h.VideoUrl,
                StartTime = h.StartTime,
                EndTime = h.EndTime,
                EventDescription = h._formatted?.eventDescription ?? h.EventDescription
            }).ToList() ?? new List<DocumentPage>(),
            TotalHits = result?.EstimatedTotalHits ?? 0,
            ProcessingTimeMs = result?.ProcessingTimeMs ?? 0
        };
    }

    public async Task DeleteDocumentAsync(string id, string? indexName = null)
    {
        var effectiveIndexName = indexName ?? _defaultIndexName;
        await InitializeAsync(effectiveIndexName);
        await _httpClient.DeleteAsync($"/indexes/{effectiveIndexName}/documents/{id}");
    }

    public async Task DeleteByResourceIdAsync(string resourceId, string? indexName = null)
    {
        var effectiveIndexName = indexName ?? _defaultIndexName;
        await InitializeAsync(effectiveIndexName);
        
        var searchParams = new { q = resourceId, limit = 100 };
        var response = await _httpClient.PostAsJsonAsync($"/indexes/{effectiveIndexName}/search", searchParams);
        var result = await response.Content.ReadFromJsonAsync<MeiliSearchResponse>();
        
        if (result?.Hits != null)
        {
            var ids = result.Hits.Select(h => h.Id).ToList();
            if (ids.Any())
            {
                var deleteContent = new { ids };
                await _httpClient.PostAsJsonAsync($"/indexes/{effectiveIndexName}/documents/delete", deleteContent);
            }
        }
    }

    private class MeiliSearchResponse
    {
        public List<MeiliHit>? Hits { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("estimatedTotalHits")]
        public int? EstimatedTotalHits { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("processingTimeMs")]
        public double? ProcessingTimeMs { get; set; }
    }

    private class MeiliHit
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("resourceId")]
        public string ResourceId { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("resourceName")]
        public string? ResourceName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pageContent")]
        public string? pageContent { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pageTitle")]
        public string? pageTitle { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("fileExtension")]
        public string? FileExtension { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("resourceType")]
        public int ResourceType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("categoryName")]
        public string? CategoryName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("uploadDate")]
        public DateTime UploadDate { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("_rankingScore")]
        public double RankingScore { get; set; }

        // Video-specific fields
        [System.Text.Json.Serialization.JsonPropertyName("videoId")]
        public string? VideoId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("videoName")]
        public string? VideoName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("videoUrl")]
        public string? VideoUrl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("startTime")]
        public string? StartTime { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("endTime")]
        public string? EndTime { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("eventDescription")]
        public string? EventDescription { get; set; }

        public MeiliFormatted? _formatted { get; set; }
    }
}
