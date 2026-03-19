using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Domain.Search.Enums;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

public class MeiliSearchService : IMeiliSearchService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<MeilisearchOptions> _options;
    private readonly IDocumentIndexRepository _documentIndexRepository;
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ICurrentTenant _currentTenant;

    public MeiliSearchService(
        IOptions<MeilisearchOptions> options,
        IDocumentIndexRepository documentIndexRepository,
        IDocumentExtractionService documentExtractionService,
        IEmbeddingService embeddingService,
        IRepository<Resource, Guid> resourceRepository,
        ICurrentTenant currentTenant,
        HttpClient httpClient)
    {
        _options = options;
        _documentIndexRepository = documentIndexRepository;
        _documentExtractionService = documentExtractionService;
        _embeddingService = embeddingService;
        _resourceRepository = resourceRepository;
        _currentTenant = currentTenant;
        
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.Value.Host);
        if (!string.IsNullOrEmpty(_options.Value.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.Value.ApiKey}");
        }
    }

    private string IndexName => _options.Value.IndexName;

    public async Task EnsureIndexExistsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"/indexes/{IndexName}");
            if (!response.IsSuccessStatusCode)
            {
                await CreateIndexAsync();
            }
        }
        catch
        {
            await CreateIndexAsync();
        }
    }

    private async Task CreateIndexAsync()
    {
        var content = new { uid = IndexName, primaryKey = "id" };
        var response = await _httpClient.PostAsJsonAsync("/indexes", content);
        response.EnsureSuccessStatusCode();

        var index = _httpClient.BaseAddress + $"/indexes/{IndexName}";
        
        await _httpClient.PostAsJsonAsync($"{index}/settings/filterable-attributes", 
            new[] { "resourceId", "resourceType", "categoryId", "fileExtension", "uploadDate", "tenantId", "status", "pageNumber" });
        
        await _httpClient.PostAsJsonAsync($"{index}/settings/searchable-attributes", 
            new[] { "pageContent", "pageTitle", "resourceName", "keywords", "description" });
        
        await _httpClient.PostAsJsonAsync($"{index}/settings/sortable-attributes", 
            new[] { "uploadDate", "pageNumber", "relevanceScore" });
    }

    public async Task<IndexTaskResultDto> IndexDocumentAsync(Guid resourceId)
    {
        var resource = await _resourceRepository.GetAsync(resourceId);
        var pages = await _documentExtractionService.ExtractPagesAsync(resourceId);

        if (!pages.Any())
        {
            return new IndexTaskResultDto
            {
                TaskId = 0,
                DocumentIndexId = Guid.Empty,
                Status = "NoContent"
            };
        }

        var tenantId = _currentTenant.Id;
        var documentIndices = new List<DocumentIndex>();
        
        foreach (var page in pages)
        {
            var docIndex = new DocumentIndex
            {
                ResourceId = resourceId,
                PageNumber = page.PageNumber,
                PageContent = page.Content,
                PageTitle = page.Title,
                IndexStatus = IndexStatus.Pending,
                TenantId = tenantId
            };
            documentIndices.Add(docIndex);
        }

        await _documentIndexRepository.InsertManyAsync(documentIndices);

        var meiliDocuments = pages.Select(p => new
        {
            id = documentIndices.First(x => x.PageNumber == p.PageNumber).Id.ToString(),
            resourceId = resourceId.ToString(),
            resourceName = resource.Name,
            resourceType = (int)resource.ResourceType,
            categoryId = resource.CategoryId?.ToString(),
            fileExtension = resource.FileExtension,
            keywords = resource.Keywords,
            description = resource.Description,
            pageNumber = p.PageNumber,
            pageContent = p.Content,
            pageTitle = p.Title,
            uploadDate = resource.CreationTime.ToString("yyyy-MM-dd"),
            tenantId = tenantId?.ToString(),
            status = (int)resource.Status,
            relevanceScore = 1.0
        }).ToList();

        var json = JsonSerializer.Serialize(meiliDocuments);
        var response = await _httpClient.PostAsync(
            $"/indexes/{IndexName}/documents",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var taskResult = await response.Content.ReadFromJsonAsync<MeiliTaskResponse>();
        
        for (int i = 0; i < documentIndices.Count; i++)
        {
            documentIndices[i].IndexingTaskId = taskResult?.TaskUid;
        }
        await _documentIndexRepository.UpdateManyAsync(documentIndices);

        return new IndexTaskResultDto
        {
            TaskId = taskResult?.TaskUid ?? 0,
            DocumentIndexId = documentIndices.First().Id,
            Status = "Processing"
        };
    }

    public async Task<IndexTaskResultDto> IndexAllPagesAsync(Guid resourceId)
    {
        return await IndexDocumentAsync(resourceId);
    }

    public async Task<SearchResultDto> SearchAsync(SearchQueryDto query)
    {
        await EnsureIndexExistsAsync();
        
        var filters = new List<string>();
        
        if (query.ResourceTypes?.Any() == true)
        {
            var typeFilters = query.ResourceTypes.Select(t => $"resourceType = {(int)t}");
            filters.Add($"({string.Join(" OR ", typeFilters)})");
        }
        
        if (query.CategoryId.HasValue)
        {
            filters.Add($"categoryId = \"{query.CategoryId}\"");
        }
        
        if (!string.IsNullOrEmpty(query.FileExtension))
        {
            filters.Add($"fileExtension = \"{query.FileExtension}\"");
        }
        
        if (query.StartDate.HasValue)
        {
            filters.Add($"uploadDate >= {query.StartDate.Value:yyyy-MM-dd}");
        }
        
        if (query.EndDate.HasValue)
        {
            filters.Add($"uploadDate <= {query.EndDate.Value:yyyy-MM-dd}");
        }

        filters.Add("status IN [2, 3]");

        var tenantId = _currentTenant.Id;
        if (tenantId.HasValue)
        {
            filters.Add($"tenantId = \"{tenantId}\"");
        }

        var searchParams = new
        {
            q = query.Query,
            limit = query.MaxResultCount,
            offset = query.SkipCount,
            filter = filters.Any() ? string.Join(" AND ", filters) : null,
            attributesToHighlight = new[] { "pageContent", "pageTitle", "resourceName" },
            highlightPreTag = "<mark>",
            highlightPostTag = "</mark>",
            attributesToCrop = new[] { "pageContent" },
            cropLength = 200,
            showRankingScore = true
        };

        var response = await _httpClient.PostAsJsonAsync($"/indexes/{IndexName}/search", searchParams);
        var result = await response.Content.ReadFromJsonAsync<MeiliSearchResponse>();

        var items = new List<DocumentSearchResultDto>();
        if (result?.Hits != null)
        {
            foreach (var hit in result.Hits)
            {
                items.Add(new DocumentSearchResultDto
                {
                    ResourceId = Guid.TryParse(hit.ResourceId, out var rid) ? rid : Guid.Empty,
                    ResourceName = hit.ResourceName ?? string.Empty,
                    PageNumber = hit.PageNumber,
                    PageContent = hit.PageContent ?? string.Empty,
                    PageTitle = hit.PageTitle,
                    HighlightedText = hit._formatted?.PageContent ?? hit.PageContent ?? string.Empty,
                    PreviewText = hit.PageContent?.Length > 200 ? hit.PageContent[..200] + "..." : hit.PageContent ?? string.Empty,
                    RelevanceScore = (float)hit.RankingScore,
                    FileExtension = hit.FileExtension ?? string.Empty,
                    ResourceType = (ResourceType)(hit.ResourceType),
                    CategoryName = hit.CategoryId,
                    UploadDate = DateTime.TryParse(hit.UploadDate, out var dt) ? dt : DateTime.MinValue
                });
            }
        }

        return new SearchResultDto
        {
            Items = items,
            TotalCount = result?.EstimatedTotalHits ?? 0,
            Query = query.Query,
            Facets = new Dictionary<string, Dictionary<string, long>>()
        };
    }

    public async Task<SearchResultDto> HybridSearchAsync(HybridSearchQueryDto query)
    {
        return await SearchAsync(query);
    }

    public async Task DeleteDocumentAsync(Guid resourceId)
    {
        var filter = $"resourceId = \"{resourceId}\"";
        var content = new { filter };
        
        await _httpClient.PostAsync($"/indexes/{IndexName}/documents/delete", 
            new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"));

        var indices = await _documentIndexRepository.GetByResourceIdAsync(resourceId);
        foreach (var docIndex in indices)
        {
            await _documentIndexRepository.DeleteAsync(docIndex);
        }
    }

    public async Task<IndexStatusDto?> GetIndexingTaskStatusAsync(long taskId)
    {
        var response = await _httpClient.GetAsync($"/tasks/{taskId}");
        var taskInfo = await response.Content.ReadFromJsonAsync<MeiliTaskInfo>();
        
        if (taskInfo?.Status == "enqueued" || taskInfo?.Status == "processing")
        {
            var pendingIndices = await _documentIndexRepository.GetByStatusAsync(
                IndexStatus.Pending, 0, 100);
            
            var index = pendingIndices.FirstOrDefault(x => x.IndexingTaskId == taskId);
            if (index != null)
            {
                return new IndexStatusDto
                {
                    DocumentIndexId = index.Id,
                    ResourceId = index.ResourceId,
                    PageNumber = index.PageNumber,
                    Status = taskInfo.Status ?? "unknown",
                    CreationTime = index.CreationTime
                };
            }
        }

        return null;
    }

    public async Task<List<IndexStatusDto>> GetPendingIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20)
    {
        var indices = await _documentIndexRepository.GetByStatusAsync(
            IndexStatus.Pending, skipCount, maxResultCount);
        
        return indices.Select(x => new IndexStatusDto
        {
            DocumentIndexId = x.Id,
            ResourceId = x.ResourceId,
            PageNumber = x.PageNumber,
            Status = x.IndexStatus.ToString(),
            CreationTime = x.CreationTime
        }).ToList();
    }

    public async Task<List<IndexStatusDto>> GetAllIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20)
    {
        var indices = await _documentIndexRepository.GetPendingIndicesAsync(skipCount, maxResultCount);
        
        return indices.Select(x => new IndexStatusDto
        {
            DocumentIndexId = x.Id,
            ResourceId = x.ResourceId,
            PageNumber = x.PageNumber,
            Status = x.IndexStatus.ToString(),
            CreationTime = x.CreationTime
        }).ToList();
    }

    public async Task RetryFailedIndexingAsync(Guid documentIndexId)
    {
        var index = await _documentIndexRepository.GetAsync(documentIndexId);
        index.IndexStatus = IndexStatus.Pending;
        await _documentIndexRepository.UpdateAsync(index);
    }
}

public class MeilisearchOptions
{
    public string Host { get; set; } = "http://localhost:7700";
    public string ApiKey { get; set; } = "";
    public string IndexName { get; set; } = "documents";
    public int EmbeddingDimension { get; set; } = 768;
}

internal class MeiliTaskResponse
{
    public long TaskUid { get; set; }
}

internal class MeiliTaskInfo
{
    public string? Status { get; set; }
}

internal class MeiliSearchResponse
{
    public List<MeiliHit>? Hits { get; set; }
    public int? EstimatedTotalHits { get; set; }
}

internal class MeiliHit
{
    public string Id { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string? ResourceName { get; set; }
    public int ResourceType { get; set; }
    public string? CategoryId { get; set; }
    public string? FileExtension { get; set; }
    public string? Keywords { get; set; }
    public string? Description { get; set; }
    public int PageNumber { get; set; }
    public string? PageContent { get; set; }
    public string? PageTitle { get; set; }
    public string? UploadDate { get; set; }
    public string? TenantId { get; set; }
    public int Status { get; set; }
    public double RankingScore { get; set; }
    public MeiliFormatted? _formatted { get; set; }
}

internal class MeiliFormatted
{
    public string? PageContent { get; set; }
    public string? PageTitle { get; set; }
    public string? ResourceName { get; set; }
}
