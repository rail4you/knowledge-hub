using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meilisearch;

namespace KnowledgeHub.Resources;

public class DocumentPage
{
    public string Id { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public string? CategoryName { get; set; }
    public DateTime UploadDate { get; set; }
}

public interface ISearchService
{
    Task InitializeAsync();
    Task AddDocumentsAsync(IEnumerable<DocumentPage> documents);
    Task<SearchResult> SearchAsync(string query, int limit = 20, int offset = 0);
    Task DeleteDocumentAsync(string id);
    Task DeleteByResourceIdAsync(string resourceId);
}

public class SearchResult
{
    public List<DocumentPage> Hits { get; set; } = new();
    public int TotalHits { get; set; }
    public double ProcessingTimeMs { get; set; }
}

public class MeiliSearchService : ISearchService
{
    private readonly MeilisearchClient _client;
    private readonly string _indexName = "documents";
    private bool _initialized = false;

    public MeiliSearchService()
    {
        var host = Environment.GetEnvironmentVariable("MEILISEARCH_HOST") ?? "http://localhost:7700";
        var apiKey = Environment.GetEnvironmentVariable("MEILISEARCH_API_KEY") ?? "aSampleMasterKey";
        _client = new MeilisearchClient(host, apiKey);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var index = _client.Index(_indexName);
            
            var filterableAttributes = new[] { "resourceId", "resourceType", "categoryName", "fileExtension" };
            var sortableAttributes = new[] { "uploadDate", "pageNumber", "resourceName" };
            var searchableAttributes = new[] { "content", "title", "resourceName" };

            await index.UpdateFilterableAttributesAsync(filterableAttributes);
            await index.UpdateSortableAttributesAsync(sortableAttributes);
            await index.UpdateSearchableAttributesAsync(searchableAttributes);

            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Meilisearch: {ex.Message}");
        }
    }

    public async Task AddDocumentsAsync(IEnumerable<DocumentPage> documents)
    {
        await InitializeAsync();
        
        var index = _client.Index(_indexName);
        await index.AddDocumentsAsync(documents, "id");
    }

    public async Task<SearchResult> SearchAsync(string query, int limit = 20, int offset = 0)
    {
        await InitializeAsync();
        
        var index = _client.Index(_indexName);
        var result = await index.SearchAsync<DocumentPage>(query, new SearchQuery
        {
            Limit = limit,
            Offset = offset,
            AttributesToHighlight = new[] { "content", "title" },
            HighlightPreTag = "<mark>",
            HighlightPostTag = "</mark>"
        });

        return new SearchResult
        {
            Hits = result.Hits.ToList(),
            TotalHits = result.EstimatedTotalHits,
            ProcessingTimeMs = result.ProcessingTimeMs
        };
    }

    public async Task DeleteDocumentAsync(string id)
    {
        await InitializeAsync();
        
        var index = _client.Index(_indexName);
        await index.DeleteDocumentsAsync(new[] { id });
    }

    public async Task DeleteByResourceIdAsync(string resourceId)
    {
        await InitializeAsync();
        
        var index = _client.Index(_indexName);
        var docs = await index.SearchAsync<DocumentPage>(resourceId, new SearchQuery { Limit = 100 });
        var ids = docs.Hits.Select(h => h.Id).ToList();
        if (ids.Any())
        {
            await index.DeleteDocumentsAsync(ids);
        }
    }
}
