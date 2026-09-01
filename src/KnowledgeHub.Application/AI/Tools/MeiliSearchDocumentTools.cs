using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Resources;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.AI.Tools;

/// <summary>
/// AI toolset for document Q&A using MeiliSearch indexes (RAG pattern).
/// Replaces the old DocumentChatTools that relied on PageIndex JSON.
///
/// Provides three tools:
///   get_document        - Document metadata (name, summary, page count, etc.)
///   get_document_structure - Page-by-page structure (pageNumber + pageTitle)
///   get_page_content    - Full text content for specific pages
///   search_document     - Full-text search across all documents or within a specific document
/// </summary>
public class MeiliSearchDocumentTools
{
    private readonly IMeiliSearchService _meiliSearchService;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ILogger _logger;

    /// <summary>
    /// When set, all queries are scoped to this resourceId.
    /// Null means search across all documents.
    /// </summary>
    private readonly Guid? _resourceId;

    /// <summary>
    /// Create tools scoped to a specific document.
    /// </summary>
    public MeiliSearchDocumentTools(
        IMeiliSearchService meiliSearchService,
        IRepository<Resource, Guid> resourceRepository,
        Guid resourceId,
        ILogger logger = null)
    {
        _meiliSearchService = meiliSearchService;
        _resourceRepository = resourceRepository;
        _resourceId = resourceId;
        _logger = logger;
    }

    /// <summary>
    /// Create tools for global cross-document search.
    /// </summary>
    public MeiliSearchDocumentTools(
        IMeiliSearchService meiliSearchService,
        IRepository<Resource, Guid> resourceRepository,
        ILogger logger = null)
    {
        _meiliSearchService = meiliSearchService;
        _resourceRepository = resourceRepository;
        _resourceId = null;
        _logger = logger;
    }

    // ================================================================
    // Tool: get_document
    // ================================================================
    [Description("获取当前文档的元数据：名称、摘要、页数、格式等。在回答文档相关问题前，先调用此工具确认文档状态。")]
    public async Task<string> GetDocument()
    {
        try
        {
            if (!_resourceId.HasValue)
            {
                return JsonSerializer.Serialize(new { error = "No document selected. Please select a document first." });
            }

            var resource = await _resourceRepository.FindAsync(_resourceId.Value);
            if (resource == null)
            {
                return JsonSerializer.Serialize(new { error = $"Document not found: {_resourceId}" });
            }

            // Count pages / segments by searching for all indexed chunks of this resource
            int pageCount = 0;
            List<DocumentSearchResultDto> indexedItems = new();
            try
            {
                var structureResult = await _meiliSearchService.SearchAsync(new SearchQueryDto
                {
                    Query = "",
                    ResourceId = _resourceId,
                    MaxResultCount = 500,
                    SkipCount = 0,
                });
                pageCount = structureResult.TotalCount;
                indexedItems = structureResult.Items;
            }
            catch
            {
                pageCount = 0;
            }

            // 对于视频或 summary 为空的资源，从索引时间轴/页面正文合成可读摘要，供 AI 总结
            string? synthesizedTimeline = null;
            List<object>? videoTimeline = null;
            string? pagePreview = null;
            var isVideo = resource.ResourceType == KnowledgeHub.Resources.Enums.ResourceType.Video
                          || IsVideoExtension(resource.FileExtension);
            if (isVideo && indexedItems.Any(x => x.SourceType == "video"))
            {
                var videoItems = indexedItems.Where(x => x.SourceType == "video")
                    .OrderBy(x => x.StartTime).Take(20).ToList();
                videoTimeline = videoItems.Select(v => new
                {
                    start_time = v.StartTime ?? "",
                    end_time = v.EndTime ?? "",
                    event_description = v.EventDescription ?? ""
                } as object).ToList();
                synthesizedTimeline = string.Join("\n", videoItems.Select(v =>
                    $"[{v.StartTime} - {v.EndTime}] {v.EventDescription}"));
            }
            else if (string.IsNullOrWhiteSpace(resource.Summary) && string.IsNullOrWhiteSpace(resource.Description) && indexedItems.Any())
            {
                // 兜底：文档类但摘要为空时，给出前几页正文预览
                var docItems = indexedItems.Where(x => x.SourceType == "document")
                    .OrderBy(x => x.PageNumber).Take(3).ToList();
                if (docItems.Count > 0)
                    pagePreview = string.Join("\n\n", docItems.Select(d => TruncateText(d.Content ?? "", 800)));
            }

            var result = new Dictionary<string, object>
            {
                ["resource_id"] = resource.Id.ToString(),
                ["doc_name"] = resource.Name,
                ["doc_description"] = resource.Description ?? "",
                ["summary"] = resource.Summary ?? resource.Description ?? "",
                ["file_extension"] = resource.FileExtension ?? "",
                ["page_count"] = pageCount,
                ["resource_type"] = resource.ResourceType.ToString(),
                ["keywords"] = resource.Keywords ?? "",
                ["status"] = "completed"
            };
            if (videoTimeline != null)
            {
                result["video_timeline"] = videoTimeline;
                result["timeline_text"] = synthesizedTimeline ?? "";
                // 覆盖 summary 供纯 get_document 总结：若原 summary 为空则用时间轴合成
                if (string.IsNullOrWhiteSpace(resource.Summary) && string.IsNullOrWhiteSpace(resource.Description))
                    result["summary"] = synthesizedTimeline ?? "";
            }
            if (pagePreview != null)
            {
                result["page_contents_preview"] = pagePreview;
                if (string.IsNullOrWhiteSpace(resource.Summary) && string.IsNullOrWhiteSpace(resource.Description))
                    result["summary"] = pagePreview;
            }

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "get_document failed for resource {ResourceId}", _resourceId);
            return JsonSerializer.Serialize(new { error = $"获取文档信息失败: {ex.Message}" });
        }
    }

    // ================================================================
    // Tool: get_document_structure
    // ================================================================
    [Description("获取文档的章节结构（标题层级树，含页码信息）。用于了解文档的组织方式，找到与用户问题相关的章节。")]
    public async Task<string> GetDocumentStructure(
        [Description("资源ID (Guid格式)。如果不提供，使用当前选中的文档。")] string resourceId = null)
    {
        try
        {
            var targetResourceId = ResolveResourceId(resourceId);
            if (targetResourceId == null)
            {
                return JsonSerializer.Serialize(new { error = "No document specified." });
            }

            var result = await _meiliSearchService.SearchAsync(new SearchQueryDto
            {
                Query = "",
                ResourceId = targetResourceId,
                MaxResultCount = 500,
                SkipCount = 0,
                Sorting = "pageNumber:asc"
            });

            if (result.TotalCount == 0)
            {
                return JsonSerializer.Serialize(new { error = "此文档尚未建立搜索索引，请先为文档生成索引。" });
            }

            // Group by title path to show heading hierarchy
            var headingGroups = result.Items
                .Where(p => !string.IsNullOrEmpty(p.Title))
                .GroupBy(p => p.Title)
                .Select(g => new
                {
                    path = g.Key,
                    page_range = $"{g.Min(p => p.PageNumber)}-{g.Max(p => p.PageNumber)}",
                    content_preview = TruncateText(g.First().Content, 120)
                })
                .OrderBy(g => g.path)
                .ToList();

            var resource = await _resourceRepository.FindAsync(targetResourceId.Value);

            var structure = new
            {
                doc_name = resource?.Name ?? "Unknown",
                total_segments = headingGroups.Count,
                headings = headingGroups
            };

            return JsonSerializer.Serialize(structure, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "get_document_structure failed");
            return JsonSerializer.Serialize(new { error = $"获取文档结构失败: {ex.Message}" });
        }
    }

    // ================================================================
    // Tool: get_page_content
    // ================================================================
    [Description("获取指定页码范围的完整正文内容。使用紧凑范围如 '5-7'（第5到7页）、'3,8'（第3和8页）、'12'（第12页）。请先调用 get_document_structure 确定相关页码范围。")]
    public async Task<string> GetPageContent(
        [Description("页码范围字符串：'5-7' 表示第5到7页，'3,8' 表示第3页和第8页，'12' 表示第12页")] string pages,
        [Description("资源ID。如果不提供，使用当前选中的文档。")] string resourceId = null)
    {
        try
        {
            var targetResourceId = ResolveResourceId(resourceId);
            if (targetResourceId == null)
            {
                return JsonSerializer.Serialize(new { error = "No document specified." });
            }

            var pageNumbers = ParsePages(pages);
            if (pageNumbers.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid pages format: {pages}" });
            }

            // Fetch all pages / timeline segments for this resource
            var result = await _meiliSearchService.SearchAsync(new SearchQueryDto
            {
                Query = "",
                ResourceId = targetResourceId,
                MaxResultCount = 500,
                SkipCount = 0,
                Sorting = "pageNumber:asc"
            });

            // 视频资源：按时间轴段序号过滤（pageNumber 均为 0，需按 order/startTime 排序后按序号切片）
            var isVideoForPaging = result.Items.Any(x => x.SourceType == "video");
            List<object> matchedPages;
            if (isVideoForPaging)
            {
                var orderedVideo = result.Items.Where(x => x.SourceType == "video")
                    .OrderBy(x => x.StartTime).ToList();
                // pages 视为段序号（1-based）
                matchedPages = orderedVideo
                    .Select((item, idx) => new { item, idx = idx + 1 })
                    .Where(x => pageNumbers.Contains(x.idx))
                    .Select(x => new
                    {
                        page_number = (object)x.idx,
                        title = (object)$"{x.item.StartTime} - {x.item.EndTime}",
                        content = (object)(x.item.EventDescription ?? ""),
                        start_time = x.item.StartTime ?? "",
                        end_time = x.item.EndTime ?? "",
                        source_type = "video"
                    } as object)
                    .ToList();
            }
            else
            {
                matchedPages = result.Items
                    .Where(p => pageNumbers.Contains(p.PageNumber))
                    .OrderBy(p => p.PageNumber)
                    .Select(p => new
                    {
                        page_number = (object)p.PageNumber,
                        title = (object)(p.Title ?? ""),
                        content = (object)(p.Content ?? ""),
                        start_time = (object?)null,
                        end_time = (object?)null,
                        source_type = "document"
                    } as object)
                    .ToList();
            }

            if (matchedPages.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = $"No content found for pages: {pages}" });
            }

            return JsonSerializer.Serialize(matchedPages, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "get_page_content failed for pages: {Pages}", pages);
            return JsonSerializer.Serialize(new { error = $"获取页面内容失败: {ex.Message}" });
        }
    }

    // ================================================================
    // Tool: search_document
    // ================================================================
    [Description("在文档内容中进行全文搜索。当用户询问文档中的某个主题、概念、知识点时使用。返回匹配的页面片段以及所属文档信息。")]
    public async Task<string> SearchDocument(
        [Description("搜索关键词")] string query,
        [Description("最大返回结果数量，默认10")] int maxResults = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new { error = "Please provide a search query." });
            }

            var searchQuery = new SearchQueryDto
            {
                Query = query,
                MaxResultCount = maxResults,
                SkipCount = 0,
            };

            // If scoped to a specific document, filter by resourceId
            if (_resourceId.HasValue)
            {
                searchQuery.ResourceId = _resourceId;
            }

            var result = await _meiliSearchService.SearchAsync(searchQuery);

            if (result.TotalCount == 0)
            {
                return JsonSerializer.Serialize(new { message = $"未找到与 \"{query}\" 相关的内容。" });
            }

            var hits = result.Items.Select(h =>
            {
                if (h.SourceType == "video")
                {
                    return new
                    {
                        resource_name = h.ResourceName,
                        resource_id = h.ResourceId,
                        source_type = "video",
                        video_name = h.VideoName ?? h.ResourceName,
                        video_url = h.VideoUrl ?? "",
                        start_time = h.StartTime ?? "",
                        end_time = h.EndTime ?? "",
                        section = $"{h.StartTime ?? ""} - {h.EndTime ?? ""}".Trim(' ', '-'),
                        page_number = 0,
                        content = h.EventDescription ?? "",
                        event_description = h.EventDescription ?? "",
                        relevance_score = Math.Round(h.RelevanceScore, 3)
                    } as object;
                }
                return new
                {
                    resource_name = h.ResourceName,
                    resource_id = h.ResourceId,
                    source_type = "document",
                    video_name = (string?)null,
                    video_url = (string?)null,
                    start_time = (string?)null,
                    end_time = (string?)null,
                    section = h.Title ?? "",
                    page_number = h.PageNumber,
                    content = h.Content ?? "",
                    event_description = (string?)null,
                    relevance_score = Math.Round(h.RelevanceScore, 3)
                } as object;
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                query,
                total_results = result.TotalCount,
                results = hits
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "search_document failed for query: {Query}", query);
            return JsonSerializer.Serialize(new { error = $"搜索失败: {ex.Message}" });
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    private Guid? ResolveResourceId(string resourceId)
    {
        if (!string.IsNullOrWhiteSpace(resourceId) && Guid.TryParse(resourceId, out var id))
        {
            return id;
        }
        return _resourceId;
    }

    private static List<int> ParsePages(string pages)
    {
        var result = new List<int>();
        foreach (var part in pages.Split(','))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.Contains('-'))
            {
                var rangeParts = trimmed.Split('-', 2);
                if (int.TryParse(rangeParts[0].Trim(), out var start) &&
                    int.TryParse(rangeParts[1].Trim(), out var end) &&
                    start <= end)
                {
                    for (var i = start; i <= end; i++)
                    {
                        result.Add(i);
                    }
                }
            }
            else if (int.TryParse(trimmed, out var pageNum))
            {
                result.Add(pageNum);
            }
        }

        return result.Distinct().OrderBy(p => p).ToList();
    }

    private static bool IsVideoExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return false;
        var normalized = ext.Trim().ToLowerInvariant();
        if (!normalized.StartsWith(".")) normalized = "." + normalized;
        return new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv", ".wmv", ".m4v", ".mpg", ".mpeg", ".3gp", ".qt" }.Contains(normalized);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var cleaned = text.Replace("\n", " ").Replace("\r", " ");
        if (cleaned.Length <= maxLength) return cleaned;
        return cleaned[..maxLength] + "...";
    }
}
