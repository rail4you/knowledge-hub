using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Search.LiteParse;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Domain.Search.Enums;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
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
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public MeiliSearchService(
        IOptions<MeilisearchOptions> options,
        IDocumentIndexRepository documentIndexRepository,
        IDocumentExtractionService documentExtractionService,
        IEmbeddingService embeddingService,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<PageContent, Guid> pageContentRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        HttpClient httpClient)
    {
        _options = options;
        _documentIndexRepository = documentIndexRepository;
        _documentExtractionService = documentExtractionService;
        _embeddingService = embeddingService;
        _resourceRepository = resourceRepository;
        _pageContentRepository = pageContentRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.Value.Host);
        if (!string.IsNullOrEmpty(_options.Value.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.Value.ApiKey}");
        }
    }

    private string IndexName => _options.Value.IndexName;

    /// <summary>
    /// 视频时间轴事件索引。schema 与 documents 不同：没有 status/categoryId/fileExtension 等字段，
    /// 但有 tenantId（租户隔离用），SearchAsync 需要在两边分别取结果再合并。
    /// </summary>
    private const string VideoIndexName = "videos";

    public async Task EnsureIndexExistsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"/indexes/{IndexName}");
            if (!response.IsSuccessStatusCode)
            {
                var content = new { uid = IndexName, primaryKey = "id" };
                response = await _httpClient.PostAsJsonAsync("/indexes", content);
                response.EnsureSuccessStatusCode();
            }
        }
        catch
        {
            var content = new { uid = IndexName, primaryKey = "id" };
            var response = await _httpClient.PostAsJsonAsync("/indexes", content);
            response.EnsureSuccessStatusCode();
        }

        await UpdateIndexSettingsAsync();

        // videos 索引也要在每次搜索时自愈：历史索引的 searchable-attributes
        // 可能是通配符 "*"（命中 id/order/startTime 等隐藏字段），导致数字
        // 查询 "1" 误召回全部视频且 _rankingScore=1.0 置顶。此前修复逻辑只
        // 放在 VideoAnalysisAppService（仅视频入库时触发），线上存量索引
        // 一直没被纠正，所以每次搜索都顺手强制同步一次。
        await EnsureVideosIndexSettingsAsync();
    }

    private async Task UpdateIndexSettingsAsync()
    {
        var index = _httpClient.BaseAddress + $"/indexes/{IndexName}";

        await _httpClient.PutAsJsonAsync($"{index}/settings/filterable-attributes",
            new[] { "resourceId", "resourceType", "categoryId", "fileExtension", "uploadDate", "tenantId", "status", "pageNumber" });

        await _httpClient.PutAsJsonAsync($"{index}/settings/searchable-attributes",
            new[] { "pageContent", "pageTitle", "resourceName", "keywords", "description" });

        await _httpClient.PutAsJsonAsync($"{index}/settings/sortable-attributes",
            new[] { "uploadDate", "pageNumber", "relevanceScore" });

        // ====== 中文搜索质量保护（2026-08-31 加固）======
        // 生产环境曾被手动改过 ranking-rules（移除 'words' 规则），导致搜索"医生"时
        // 把大量不含 query 词的文档也返回（基于 attributeRank 等次要规则凑分）。
        // 在这里强制设置默认 ranking-rules，确保每次重建索引时 settings 保持正确。
        await _httpClient.PutAsJsonAsync($"{index}/settings/ranking-rules", new[]
        {
            "words",        // 必须放第一位：匹配 query 词的百分比，剔除不相关文档
            "typo",
            "proximity",
            "attribute",
            "sort",
            "exactness"
        });

        // typo-tolerance 保持默认：中文 5 字以下不做 typo，避免"医/以"这种字符级错配
        await _httpClient.PutAsJsonAsync($"{index}/settings/typo-tolerance", new
        {
            enabled = true,
            minWordSizeForTypos = new { oneTypo = 5, twoTypos = 9 }
        });

        // prefix-search 保持 indexingTime：用户输入时即时补全，但 ranking 仍然受 words 约束
        await _httpClient.PutAsJsonAsync($"{index}/settings/prefix-search", "indexingTime");

        // proximityPrecision 用 byAttribute：按属性而不是按字计算 proximity，召回更准
        await _httpClient.PutAsJsonAsync($"{index}/settings/proximity-precision", "byAttribute");
    }

    /// <summary>
    /// 强制同步 videos 索引的 settings（不存在则创建）。与
    /// VideoAnalysisAppService.EnsureVideosIndexExistsAsync 保持一致。
    /// 最佳努力：失败不抛异常，不能拖垮 documents 侧的正常搜索。
    /// </summary>
    private async Task EnsureVideosIndexSettingsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"/indexes/{VideoIndexName}");
            if (!response.IsSuccessStatusCode)
            {
                var content = new { uid = VideoIndexName, primaryKey = "id" };
                response = await _httpClient.PostAsJsonAsync("/indexes", content);
                response.EnsureSuccessStatusCode();
            }

            var index = _httpClient.BaseAddress + $"/indexes/{VideoIndexName}";

            // 关键：searchable 只保留业务文本字段。id/order/startTime/endTime
            // 等一旦可搜，数字查询（如 "1"）就会误命中 GUID/序号/时间戳。
            await _httpClient.PutAsJsonAsync($"{index}/settings/searchable-attributes",
                new[] { "videoName", "eventDescription" });

            await _httpClient.PutAsJsonAsync($"{index}/settings/filterable-attributes",
                new[] { "resourceId", "videoId", "videoName", "tenantId", "indexedAt" });

            await _httpClient.PutAsJsonAsync($"{index}/settings/sortable-attributes",
                new[] { "order", "indexedAt", "startTime" });

            // words 必须第一位：不含 query 词的文档直接剔除
            await _httpClient.PutAsJsonAsync($"{index}/settings/ranking-rules", new[]
            {
                "words",
                "typo",
                "proximity",
                "attribute",
                "sort",
                "exactness"
            });
        }
        catch
        {
            // videos 索引自愈失败不影响 documents 搜索结果合并
        }
    }

    public async Task<IndexTaskResultDto> IndexDocumentAsync(Guid resourceId)
    {
        Resource? resource;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var q = await _resourceRepository.GetQueryableAsync();
            resource = await q.Where(x => x.Id == resourceId).AsNoTracking().FirstOrDefaultAsync();
        }
        if (resource == null) return new IndexTaskResultDto { TaskId = 0, DocumentIndexId = Guid.Empty, Status = "NotFound" };
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

        // 设置 IndexingTaskId（在 insert 之前设置，避免 insert 后 update 触发 ConcurrencyStamp 冲突）
        foreach (var di in documentIndices)
        {
            di.IndexingTaskId = 0;
        }
        await _documentIndexRepository.InsertManyAsync(documentIndices);

        // 同时写入 PageContent（用于 hasPageIndex 判断）
        var pageContents = pages.Select(p => new PageContent
        {
            ResourceId = resourceId,
            PageNumber = p.PageNumber,
            Content = p.Content ?? string.Empty,
            PageWidth = 595f,
            PageHeight = 842f,
            TenantId = tenantId
        }).ToList();
        await _pageContentRepository.InsertManyAsync(pageContents);

        var meiliDocuments = pages.Select(p => new
        {
            id = documentIndices.First(x => x.PageNumber == p.PageNumber).Id.ToString(),
            resourceId = resourceId.ToString(),
            resourceName = resource.Name ?? "",
            resourceType = (int)resource.ResourceType,
            categoryId = resource.CategoryId?.ToString() ?? "",
            fileExtension = resource.FileExtension ?? "",
            keywords = resource.Keywords ?? "",
            description = resource.Description ?? "",
            pageNumber = p.PageNumber,
            pageContent = p.Content ?? "",
            pageTitle = p.Title ?? "",
            uploadDate = resource.CreationTime.ToString("yyyy-MM-dd"),
            tenantId = tenantId?.ToString() ?? "",
            status = (int)resource.Status,
            relevanceScore = 1.0
        }).ToList();

        var json = JsonSerializer.Serialize(meiliDocuments);
        var response = await _httpClient.PostAsync(
            $"/indexes/{IndexName}/documents",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var taskResult = await response.Content.ReadFromJsonAsync<MeiliTaskResponse>();

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

    public async Task<IndexTaskResultDto> IndexDocumentFromPagesAsync(Guid resourceId, List<PageContentDto> pages)
    {
        await EnsureIndexExistsAsync();
        
        Resource? resource;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.FindAsync(resourceId);
        }
        if (resource == null) return new IndexTaskResultDto { TaskId = 0, DocumentIndexId = Guid.Empty, Status = "NotFound" };

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

        // Delete old DocumentIndex records for this resource
        var oldIndices = await _documentIndexRepository.GetByResourceIdAsync(resourceId);
        if (oldIndices.Any())
        {
            await _documentIndexRepository.DeleteManyAsync(oldIndices);
        }

        // Create new DocumentIndex entities
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

        // Build Meilisearch documents and POST
        var meiliDocuments = pages.Select(p => new
        {
            id = documentIndices.First(x => x.PageNumber == p.PageNumber).Id.ToString(),
            resourceId = resourceId.ToString(),
            resourceName = resource.Name ?? "",
            resourceType = (int)resource.ResourceType,
            categoryId = resource.CategoryId?.ToString() ?? "",
            fileExtension = resource.FileExtension ?? "",
            keywords = resource.Keywords ?? "",
            description = resource.Description ?? "",
            pageNumber = p.PageNumber,
            pageContent = p.Content ?? "",
            pageTitle = p.Title ?? "",
            uploadDate = resource.CreationTime.ToString("yyyy-MM-dd"),
            tenantId = tenantId?.ToString() ?? "",
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

    public async Task<SearchResultDto> SearchAsync(SearchQueryDto query)
    {
        await EnsureIndexExistsAsync();

        // 调用者明确指定了 IndexName（前端索引下拉框）则只走单边，
        // 与 HybridSearchAsync 保持一致。之前这里忽略 IndexName 总是合并
        // 双索引，导致选“文档”时视频结果仍以 1.0 满分置顶（如 ?q=1）。
        if (!string.IsNullOrEmpty(query.IndexName))
        {
            var single = await ExecuteSingleIndexSearchAsync(
                query.IndexName!, query,
                applyDocumentFilters: string.Equals(query.IndexName, IndexName, StringComparison.OrdinalIgnoreCase),
                hybrid: false);
            return new SearchResultDto
            {
                Items = single.Items,
                TotalCount = single.Total,
                Query = query.Query,
                Facets = new Dictionary<string, Dictionary<string, long>>()
            };
        }

        // 同时搜两个索引：documents（文档/PDF/PPT等） 和 videos（视频时间轴事件）。
        // 两套 schema 不同：videos 没有 status/fileExtension/categoryId，
        // 所以只在 documents 侧应用这些 filter；tenantId 两个索引都有，统一应用。
        var docTask = ExecuteSingleIndexSearchAsync(IndexName, query, applyDocumentFilters: true, hybrid: false);
        var vidTask = ExecuteSingleIndexSearchAsync(VideoIndexName, query, applyDocumentFilters: false, hybrid: false);

        await Task.WhenAll(docTask, vidTask);

        var (docItems, docTotal) = docTask.Result;
        var (vidItems, vidTotal) = vidTask.Result;

        // 按 RelevanceScore 合并取 top N
        var merged = docItems.Concat(vidItems)
            .OrderByDescending(x => x.RelevanceScore)
            .Take(Math.Max(query.MaxResultCount, 0))
            .ToList();

        return new SearchResultDto
        {
            Items = merged,
            TotalCount = docTotal + vidTotal,
            Query = query.Query,
            Facets = new Dictionary<string, Dictionary<string, long>>()
        };
    }

    public async Task<SearchResultDto> HybridSearchAsync(HybridSearchQueryDto query)
    {
        await EnsureIndexExistsAsync();

        // 如果调用者明确指定了 IndexName（videos 或 documents）则只走单边，
        // 默认不指定则同时搜两个索引，合并结果。
        if (!string.IsNullOrEmpty(query.IndexName))
        {
            var (items, total) = await ExecuteSingleIndexSearchAsync(
                query.IndexName!, query, applyDocumentFilters: query.IndexName == IndexName, hybrid: true);
            return new SearchResultDto
            {
                Items = items,
                TotalCount = total,
                Query = query.Query,
                Facets = new Dictionary<string, Dictionary<string, long>>()
            };
        }

        var docTask = ExecuteSingleIndexSearchAsync(IndexName, query, applyDocumentFilters: true, hybrid: true);
        var vidTask = ExecuteSingleIndexSearchAsync(VideoIndexName, query, applyDocumentFilters: false, hybrid: true);

        await Task.WhenAll(docTask, vidTask);

        var (docItems, docTotal) = docTask.Result;
        var (vidItems, vidTotal) = vidTask.Result;

        var merged = docItems.Concat(vidItems)
            .OrderByDescending(x => x.RelevanceScore)
            .Take(Math.Max(query.MaxResultCount, 0))
            .ToList();

        return new SearchResultDto
        {
            Items = merged,
            TotalCount = docTotal + vidTotal,
            Query = query.Query,
            Facets = new Dictionary<string, Dictionary<string, long>>()
        };
    }

    /// <summary>
    /// 在指定 Meilisearch 索引上执行一次搜索，返回 (items, total)。
    /// applyDocumentFilters=false 时不加 status/categoryId/fileExtension 等文档专属 filter，
    /// 适用于视频索引；但租户隔离（tenantId）对两个索引统一生效。
    /// </summary>
    private async Task<(List<DocumentSearchResultDto> Items, int Total)> ExecuteSingleIndexSearchAsync(
        string indexName, SearchQueryDto query, bool applyDocumentFilters, bool hybrid)
    {
        var filters = new List<string>();

        if (applyDocumentFilters)
        {
            if (query.FileExtensions?.Any() == true)
            {
                var extFilters = query.FileExtensions.Select(ext => $"fileExtension = \"{ext}\"");
                filters.Add($"({string.Join(" OR ", extFilters)})");
            }

            if (query.CategoryId.HasValue)
            {
                filters.Add($"categoryId = \"{query.CategoryId}\"");
            }

            if (query.StartDate.HasValue)
            {
                filters.Add($"uploadDate >= \"{query.StartDate.Value:yyyy-MM-dd}\"");
            }

            if (query.EndDate.HasValue)
            {
                filters.Add($"uploadDate <= \"{query.EndDate.Value:yyyy-MM-dd}\"");
            }

            filters.Add(!string.IsNullOrWhiteSpace(query.StatusFilter)
                ? $"status IN [{query.StatusFilter}]"
                : "status IN [0, 1, 2, 3]");
        }

        // 租户隔离：documents 与 videos 两个索引统一应用，严格只返回当前租户的资源。
        // Host 用户（CurrentTenant 为空）不过滤，可见全部，用于平台管理。
        // NOT EXISTS 用于兼容历史上未写入 tenantId 的旧视频索引文档：这些文档先被放行，
        // 随后在 FilterStaleResourceHitsAsync 中以数据库 Resource.TenantId 为准兜底过滤，
        // 非本租户的命中会被丢弃，因此不会造成跨租户数据泄漏。
        var tenantId = _currentTenant.Id;
        if (tenantId.HasValue)
        {
            filters.Add($"(tenantId = \"{tenantId}\" OR tenantId NOT EXISTS)");
        }

        if (query.ResourceId.HasValue)
        {
            filters.Add($"resourceId = \"{query.ResourceId}\"");
        }

        object searchParams;
        if (hybrid)
        {
            searchParams = new
            {
                q = query.Query,
                limit = query.MaxResultCount,
                offset = query.SkipCount,
                filter = filters.Any() ? string.Join(" AND ", filters) : null,
                attributesToHighlight = new[] { "pageContent", "pageTitle", "resourceName", "eventDescription", "videoName" },
                highlightPreTag = "<mark>",
                highlightPostTag = "</mark>",
                attributesToCrop = new[] { "pageContent" },
                cropLength = 200,
                showRankingScore = true,
                hybrid = new
                {
                    embedder = "qwen",
                    semanticRatio = 1.0
                }
            };
        }
        else
        {
            searchParams = new
            {
                q = query.Query,
                limit = query.MaxResultCount,
                offset = query.SkipCount,
                filter = filters.Any() ? string.Join(" AND ", filters) : null,
                attributesToHighlight = new[] { "pageContent", "pageTitle", "resourceName", "eventDescription", "videoName" },
                highlightPreTag = "<mark>",
                highlightPostTag = "</mark>",
                attributesToCrop = new[] { "pageContent" },
                cropLength = 200,
                showRankingScore = true
            };
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync($"/indexes/{indexName}/search", searchParams);
        }
        catch (Exception ex)
        {
            // 单个索引搜索失败不能拖垮另一个索引的合并结果。
            // 比如客户端调用方传递了不存在的可过滤属性，Meilisearch 会报 4xx。
            return (new List<DocumentSearchResultDto>(), 0);
        }

        if (!response.IsSuccessStatusCode)
        {
            // videos 索引可能缺一些 filterable attributes；本次调用仅触发器记录一次，
            // 调用方仍能看到合并后的结果。
            return (new List<DocumentSearchResultDto>(), 0);
        }

        var result = await response.Content.ReadFromJsonAsync<MeiliSearchResponse>();

        var items = new List<DocumentSearchResultDto>();
        if (result?.Hits != null)
        {
            foreach (var hit in result.Hits)
            {
                items.Add(new DocumentSearchResultDto
                {
                    ResourceId = hit.ResourceId ?? string.Empty,
                    ResourceName = hit.ResourceName ?? hit.VideoName ?? string.Empty,
                    PageNumber = hit.PageNumber,
                    Content = hit.PageContent ?? string.Empty,
                    Title = hit.PageTitle,
                    HighlightedContent = hit._formatted?.PageContent ?? hit.PageContent ?? string.Empty,
                    RelevanceScore = (float)hit.RankingScore,
                    FileExtension = hit.FileExtension ?? string.Empty,
                    ResourceType = (ResourceType)(hit.ResourceType),
                    CategoryName = hit.CategoryId,
                    UploadDate = DateTime.TryParse(hit.UploadDate, out var dt) ? dt : DateTime.MinValue,

                    // Video-specific fields
                    SourceType = !string.IsNullOrEmpty(hit.VideoId) ? "video" : "document",
                    VideoId = hit.VideoId,
                    VideoName = hit.VideoName,
                    VideoUrl = hit.VideoUrl,
                    StartTime = hit.StartTime,
                    EndTime = hit.EndTime,
                    EventDescription = hit._formatted?.EventDescription ?? hit.EventDescription
                });
            }
        }

        // Meili 索引可能残留已删除资源的文档（删除时 Meili 不可用、DB 重建等），
        // 其 resourceId 在 AppResources 中已不存在。继续返回会导致前端
        // “返回资源” 404（不存在 id=... 的实体 Resource!），以及 log-view
        // 写入 KhResourceViewLogs/KhResourceExposures 时外键冲突 500。
        // 这里批量校验存在性，直接丢弃幽灵命中。
        var rawCount = items.Count;
        items = await FilterStaleResourceHitsAsync(items);
        var total = (int)(result?.EstimatedTotalHits ?? 0) - (rawCount - items.Count);

        return (items, Math.Max(total, 0));
    }

    /// <summary>
    /// 丢弃 resourceId 在数据库中已不存在的命中。
    /// 同时以数据库 Resource.TenantId 为准做租户兜底校验（防御纵深）：
    /// 即便 Meili 索引里的 tenantId 字段缺失或写错，也不会把其它租户的资源返回给当前租户。
    /// </summary>
    private async Task<List<DocumentSearchResultDto>> FilterStaleResourceHitsAsync(List<DocumentSearchResultDto> items)
    {
        if (items.Count == 0) return items;

        var ids = items
            .Select(x => x.ResourceId)
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new List<DocumentSearchResultDto>();

        // 禁用 ABP 多租户过滤器后自行按数据库 TenantId 校验，逻辑显式且可覆盖历史脏数据。
        List<Resource> existing;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            existing = await _resourceRepository.GetListAsync(x => ids.Contains(x.Id));
        }

        // 严格租户隔离：租户用户只保留本租户资源，Host 资源（TenantId 为空）不可见；
        // Host 用户（CurrentTenant 为空）可见全部。
        var currentTenantId = _currentTenant.Id;
        var alive = new HashSet<Guid>(
            existing
                .Where(r => !currentTenantId.HasValue || r.TenantId == currentTenantId.Value)
                .Select(r => r.Id));

        return items
            .Where(x => Guid.TryParse(x.ResourceId, out var g) && alive.Contains(g))
            .ToList();
    }

    public async Task DeleteDocumentAsync(Guid resourceId)
    {
        var filter = $"resourceId = \"{resourceId}\"";
        var content = new { filter };
        
        await _httpClient.PostAsync($"/indexes/{IndexName}/documents/delete", 
            new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"));

        // 同步清理视频时间轴索引（videoIndex 的 resourceId 与 document 同字段）
        try
        {
            await _httpClient.PostAsync($"/indexes/{VideoIndexName}/documents/delete",
                new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"));
        }
        catch { /* videos 索引可能不存在或无该资源，静默忽略 */ }

        var indices = await _documentIndexRepository.GetByResourceIdAsync(resourceId);
        foreach (var docIndex in indices)
        {
            await _documentIndexRepository.DeleteAsync(docIndex);
        }
    }

    public async Task<IndexTaskResultDto> RefreshDocumentIndexAsync(Guid resourceId)
    {
        var embedderConfig = new
        {
            qwen = new
            {
                source = "rest",
                url = "https://dashscope.aliyuncs.com/compatible-mode/v1/embeddings",
                apiKey = _options.Value.EmbeddingApiKey,
                dimensions = _options.Value.EmbeddingDimension,
                documentTemplate = "{{doc.pageTitle}} {{doc.pageContent}}",
                request = new
                {
                    model = "text-embedding-v3",
                    input = "{{text}}",
                    encoding_format = "float"
                },
                response = new
                {
                    data = new[]
                    {
                        new { embedding = "{{embedding}}" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(embedderConfig);
        var response = await _httpClient.PatchAsync(
            $"/indexes/{IndexName}/settings/embedders",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var taskResult = await response.Content.ReadFromJsonAsync<MeiliTaskResponse>();

        return new IndexTaskResultDto
        {
            TaskId = taskResult?.TaskUid ?? 0,
            DocumentIndexId = resourceId,
            Status = "Processing"
        };
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

    public async Task<List<HotWordDto>> GetHotWordsAsync(Guid resourceId, int count = 30)
    {
        string? combinedText = null;
        try
        {
            // 优先从 MeiliSearch 拉取该资源的所有页面内容
            var searchResult = await SearchAsync(new SearchQueryDto
            {
                Query = "",
                ResourceId = resourceId,
                MaxResultCount = 500,
                SkipCount = 0,
                Sorting = "pageNumber:asc"
            });

            if (searchResult.TotalCount > 0)
            {
                combinedText = string.Join("\n", searchResult.Items.Select(p =>
                    !string.IsNullOrWhiteSpace(p.Content) ? p.Content :
                    !string.IsNullOrWhiteSpace(p.EventDescription) ? p.EventDescription! : string.Empty));
            }
        }
        catch (Exception ex)
        {
            // Meili 失败则回退到数据库
        }

        // 回退：直接从数据库 PageContents 读取（应对索引缺失/Meili 不可用场景）
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            try
            {
                List<PageContent> pages;
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    pages = await _pageContentRepository.GetListAsync(x => x.ResourceId == resourceId);
                }
                if (pages.Any())
                {
                    combinedText = string.Join("\n", pages.OrderBy(p => p.PageNumber).Select(p => p.Content));
                }
            }
            catch
            {
                // 忽略，回退失败则返回空
            }
        }

        if (string.IsNullOrWhiteSpace(combinedText))
        {
            return [];
        }

        // 初筛时多取一些，过一轮可检索性校验（Meilisearch 能否召回）后再截断到 count，
        // 确保 /ai/chat 热门词点击后“在文档中搜索关于“xxx”的内容”一定有结果，
        // 避免“本概念/成的三”这类 n-gram 碎片虽在原文出现却因召回分词不一致而搜不到。
        var candidates = ChineseTextTokenizer.ExtractHotWords(combinedText, count * 2 + 10);

        // 可检索性校验：仅保留在当前资源下 Meilisearch 能召回的词
        var validated = new List<HotWordDto>();
        foreach (var hw in candidates)
        {
            try
            {
                var r = await SearchAsync(new SearchQueryDto
                {
                    Query = hw.Word,
                    ResourceId = resourceId,
                    MaxResultCount = 1,
                    SkipCount = 0
                });
                if (r.TotalCount > 0)
                {
                    validated.Add(new HotWordDto { Word = hw.Word, Frequency = hw.Frequency });
                    if (validated.Count >= count) break;
                }
            }
            catch
            {
                // 校验失败（索引不可用等）则保守保留，避免热门词面板完全空白
                validated.Add(new HotWordDto { Word = hw.Word, Frequency = hw.Frequency });
                if (validated.Count >= count) break;
            }
        }

        // 不再用不可召回的候选补齐——宁可返回更少但可搜的有效词，也比“理表现”这类搜不到的碎片好
        return validated.Take(count).ToList();
    }
}

public class MeilisearchOptions
{
    public string Host { get; set; } = "http://localhost:7700";
    public string ApiKey { get; set; } = "";
    public string IndexName { get; set; } = "documents";
    public int EmbeddingDimension { get; set; } = 768;
    public string? EmbeddingApiKey { get; set; }
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
    [System.Text.Json.Serialization.JsonPropertyName("_rankingScore")]
    public double RankingScore { get; set; }

    // Video-specific fields (populated for video index hits)
    public string? VideoId { get; set; }
    public string? VideoName { get; set; }
    public string? VideoUrl { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? EventDescription { get; set; }

    public MeiliFormatted? _formatted { get; set; }
}

internal class MeiliFormatted
{
    public string? PageContent { get; set; }
    public string? PageTitle { get; set; }
    public string? ResourceName { get; set; }

    // Video-specific highlighted fields
    public string? EventDescription { get; set; }
    public string? VideoName { get; set; }
}
