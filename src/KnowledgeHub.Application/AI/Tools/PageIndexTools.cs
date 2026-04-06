using System;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.AI.Tools;

public class PageIndexTools : ITransientDependency
{
    private readonly IPageIndexService _pageIndexService;
    private readonly ILogger<PageIndexTools> _logger;

    public PageIndexTools(
        IPageIndexService pageIndexService,
        ILogger<PageIndexTools> logger)
    {
        _pageIndexService = pageIndexService;
        _logger = logger;
    }

    [Description("搜索文档的页面索引结构。当用户询问文档目录、章节、内容结构时使用此工具。返回匹配的章节标题和摘要。")]
    public async Task<string> SearchPageIndex(
        [Description("搜索关键词，用于匹配文档章节的标题和摘要")] string query,
        [Description("最大返回结果数量，默认5")] int maxResults = 5)
    {
        try
        {
            var results = await _pageIndexService.SearchPageIndexAsync(query, maxResults);

            if (results.Count == 0)
            {
                return $"未找到与 \"{query}\" 相关的文档章节。";
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchPageIndex tool failed for query: {Query}", query);
            return $"搜索失败: {ex.Message}";
        }
    }

    [Description("获取指定文档的完整页面索引树。当用户想了解某文档的详细结构（目录、章节层次）时使用。")]
    public async Task<string> GetDocumentStructure(
        [Description("资源ID (Guid格式)")] string resourceId)
    {
        try
        {
            if (!Guid.TryParse(resourceId, out var id))
            {
                return "无效的资源ID格式，请提供有效的Guid。";
            }

            var pageIndex = await _pageIndexService.GetPageIndexAsync(id);
            if (pageIndex == null)
            {
                return $"未找到资源 {resourceId} 的页面索引数据。可能该资源尚未生成索引，或不支持该文件格式。";
            }

            var summary = new
            {
                pageIndex.SourceFormat,
                pageIndex.NodeCount,
                pageIndex.Model,
                Structure = pageIndex.PageIndexJson
            };

            return JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDocumentStructure tool failed for resourceId: {ResourceId}", resourceId);
            return $"获取文档结构失败: {ex.Message}";
        }
    }
}
