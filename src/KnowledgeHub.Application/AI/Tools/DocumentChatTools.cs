using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Application.AI.Tools;

public class DocumentChatTools
{
    private readonly JsonDocument _pageIndexDoc;
    private readonly ILogger _logger;

    public DocumentChatTools(string pageIndexJson, ILogger logger = null)
    {
        _pageIndexDoc = JsonDocument.Parse(pageIndexJson);
        _logger = logger;
    }

    [Description("Get document metadata: doc_name, doc_description, page_count, etc.")]
    public string GetDocument()
    {
        var root = _pageIndexDoc.RootElement;
        var result = new Dictionary<string, object>
        {
            ["doc_name"] = root.TryGetProperty("doc_name", out var name) ? (object)name.GetString() : null,
            ["doc_description"] = root.TryGetProperty("doc_description", out var desc) ? (object)desc.GetString() : null,
            ["status"] = "completed"
        };

        var maxPage = 0;
        if (root.TryGetProperty("structure", out var structure))
        {
            maxPage = FindMaxPage(structure);
        }

        result["page_count"] = maxPage;

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    [Description("Get the document's full tree structure (title, node_id, start_index, end_index) without text content. Use this to identify relevant page ranges before calling get_page_content.")]
    public string GetDocumentStructure()
    {
        var root = _pageIndexDoc.RootElement;
        if (!root.TryGetProperty("structure", out var structure))
        {
            return JsonSerializer.Serialize(new { error = "No structure found in document" });
        }

        var structureNoText = RemoveFields(structure, new HashSet<string> { "text", "summary" });
        return JsonSerializer.Serialize(structureNoText, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    [Description("Get the text content of specific pages. Use tight ranges like '5-7' for pages 5 to 7, '3,8' for pages 3 and 8, '12' for page 12. Always call get_document_structure first to identify relevant page ranges.")]
    public string GetPageContent(
        [Description("Page range string: '5-7' for range, '3,8' for individual pages, '12' for single page")] string pages)
    {
        try
        {
            var pageNumbers = ParsePages(pages);
            if (pageNumbers.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid pages format: {pages}" });
            }

            var root = _pageIndexDoc.RootElement;
            if (!root.TryGetProperty("structure", out var structure))
            {
                return JsonSerializer.Serialize(new { error = "No structure found in document" });
            }

            var results = new List<Dictionary<string, object>>();
            CollectContentForPages(structure, pageNumbers, results);

            if (results.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = $"No content found for pages: {pages}" });
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "GetPageContent failed for pages: {Pages}", pages);
            return JsonSerializer.Serialize(new { error = $"Failed to get page content: {ex.Message}" });
        }
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

    private static void CollectContentForPages(
        JsonElement structure,
        List<int> pageNumbers,
        List<Dictionary<string, object>> results)
    {
        foreach (var node in structure.EnumerateArray())
        {
            var startIdx = node.TryGetProperty("start_index", out var si) ? si.GetInt32() : 0;
            var endIdx = node.TryGetProperty("end_index", out var ei) ? ei.GetInt32() : 0;

            var hasOverlap = pageNumbers.Any(p => p >= startIdx && p <= endIdx);
            if (hasOverlap)
            {
                var entry = new Dictionary<string, object>
                {
                    ["title"] = node.TryGetProperty("title", out var t) ? (object)t.GetString() : null,
                    ["node_id"] = node.TryGetProperty("node_id", out var nid) ? (object)nid.GetString() : null,
                    ["start_index"] = startIdx,
                    ["end_index"] = endIdx,
                    ["text"] = node.TryGetProperty("text", out var text) ? (object)text.GetString() : null,
                    ["summary"] = node.TryGetProperty("summary", out var summary) ? (object)summary.GetString() : null,
                };
                results.Add(entry);
            }

            var childrenProp = node.TryGetProperty("nodes", out var nodes)
                ? nodes
                : node.TryGetProperty("children", out var children)
                    ? children
                    : default;

            if (childrenProp.ValueKind == JsonValueKind.Array)
            {
                CollectContentForPages(childrenProp, pageNumbers, results);
            }
        }
    }

    private static int FindMaxPage(JsonElement structure)
    {
        var max = 0;
        foreach (var node in structure.EnumerateArray())
        {
            if (node.TryGetProperty("end_index", out var ei))
            {
                var val = ei.GetInt32();
                if (val > max) max = val;
            }

            var childrenProp = node.TryGetProperty("nodes", out var nodes)
                ? nodes
                : node.TryGetProperty("children", out var children)
                    ? children
                    : default;

            if (childrenProp.ValueKind == JsonValueKind.Array)
            {
                var childMax = FindMaxPage(childrenProp);
                if (childMax > max) max = childMax;
            }
        }

        return max;
    }

    private static JsonElement RemoveFields(JsonElement element, HashSet<string> fieldsToRemove)
    {
        return RemoveFieldsInternal(element, fieldsToRemove);
    }

    private static JsonElement RemoveFieldsInternal(JsonElement element, HashSet<string> fieldsToRemove)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var items = new List<JsonElement>();
            foreach (var item in element.EnumerateArray())
            {
                items.Add(RemoveFieldsInternal(item, fieldsToRemove));
            }

            return JsonDocument.Parse(JsonSerializer.Serialize(items)).RootElement;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, JsonElement>();
            foreach (var prop in element.EnumerateObject())
            {
                if (fieldsToRemove.Contains(prop.Name)) continue;

                if (prop.Name == "nodes" || prop.Name == "children")
                {
                    var cleaned = RemoveFieldsInternal(prop.Value, fieldsToRemove);
                    dict[prop.Name] = cleaned;
                }
                else
                {
                    dict[prop.Name] = prop.Value;
                }
            }

            return JsonDocument.Parse(JsonSerializer.Serialize(dict)).RootElement;
        }

        return element;
    }
}
