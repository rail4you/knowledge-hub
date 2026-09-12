using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.AI.Summary;

public class DocumentSummaryService : IDocumentSummaryService
{
    private const int HeadPages = 5;
    private const int TailPages = 5;
    private const int MiddleSamples = 5;
    private const int MaxCharsPerPage = 1500;
    private const int MaxTotalChars = 60_000;
    private const int MaxSummaryLength = 4000;
    private const int MaxKeywords = 10;

    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly QwenSummaryClient _qwenClient;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<DocumentSummaryService> _logger;
    private readonly IAiUsageTracker _usageTracker;

    private const string SystemPrompt = @"你是文档摘要与关键词提取助手。
严格要求：
1. 必须输出有效 JSON（不要用 markdown 代码块包裹）
2. summary 不超过 200 字，使用第三人称
3. keywords 是 5-10 个中文词或短语，英文逗号分隔
4. 输出语言与文档主要语言一致
JSON 结构：{ ""summary"": ""..."", ""keywords"": ""..."" }";

    public DocumentSummaryService(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<PageContent, Guid> pageContentRepository,
        QwenSummaryClient qwenClient,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        ILogger<DocumentSummaryService> logger,
        IAiUsageTracker usageTracker)
    {
        _resourceRepository = resourceRepository;
        _pageContentRepository = pageContentRepository;
        _qwenClient = qwenClient;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _logger = logger;
        _usageTracker = usageTracker;
    }

    public async Task GenerateAndPersistAsync(DocumentSummaryGenerationJobArgs args)
    {
        using (_currentTenant.Change(args.TenantId))
        {
            Resource? resource;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                resource = await _resourceRepository.FindAsync(args.ResourceId);
            }
            if (resource == null)
            {
                throw new UserFriendlyException($"Resource not found: {args.ResourceId}");
            }

            // 读取页面内容（按页码升序）
            List<PageContent> pages;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                pages = await _pageContentRepository.GetListAsync(p => p.ResourceId == args.ResourceId);
            }
            pages = pages.OrderBy(p => p.PageNumber).ToList();

            if (pages.Count == 0)
            {
                _logger.LogWarning(
                    "No page content found for resource {ResourceId}, skipping summary generation",
                    args.ResourceId);
                return;
            }

            var truncatedText = BuildSampledText(pages);
            var userPrompt = BuildUserPrompt(resource, truncatedText);

            _logger.LogInformation(
                "Calling Qwen to generate summary for resource {ResourceId} ({PageCount} pages, {CharCount} chars sampled)",
                args.ResourceId, pages.Count, truncatedText.Length);

            var usageId = await _usageTracker.StartAsync(
                AiFeatureGroups.Summary, "DocSummary", _qwenClient.ResolvedModel,
                SystemPrompt + "\n" + userPrompt, tenantId: args.TenantId);
            string rawResponse;
            try
            {
                rawResponse = await _qwenClient.CompleteAsync(SystemPrompt, userPrompt);
                await _usageTracker.CompleteAsync(usageId, rawResponse, true);
            }
            catch (Exception ex)
            {
                await _usageTracker.CompleteAsync(usageId, null, false, ex.Message);
                throw;
            }

            var (summary, keywords) = ParseResponse(rawResponse);

            // 写入 Resource
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbResource = await _resourceRepository.GetAsync(args.ResourceId);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    dbResource.Summary = summary.Length > MaxSummaryLength
                        ? summary.Substring(0, MaxSummaryLength)
                        : summary;
                }
                if (!string.IsNullOrWhiteSpace(keywords))
                {
                    dbResource.Keywords = keywords;
                }
                await _resourceRepository.UpdateAsync(dbResource);
            }

            _logger.LogInformation(
                "Summary generated for resource {ResourceId}: {SummaryLen} chars summary, {KeywordCount} keywords",
                args.ResourceId, summary?.Length ?? 0,
                string.IsNullOrEmpty(keywords) ? 0 : keywords.Split(',').Length);
        }
    }

    private static string BuildSampledText(List<PageContent> pages)
    {
        var sb = new System.Text.StringBuilder();
        var picked = PickPages(pages);

        foreach (var page in picked)
        {
            var content = page.Content ?? string.Empty;
            if (content.Length > MaxCharsPerPage)
            {
                content = content.Substring(0, MaxCharsPerPage);
            }
            sb.AppendLine($"[第 {page.PageNumber} 页]");
            sb.AppendLine(content);
            sb.AppendLine();

            if (sb.Length >= MaxTotalChars)
            {
                break;
            }
        }

        if (sb.Length > MaxTotalChars)
        {
            return sb.ToString(0, MaxTotalChars);
        }
        return sb.ToString();
    }

    private static List<PageContent> PickPages(List<PageContent> pages)
    {
        if (pages.Count <= HeadPages + TailPages + MiddleSamples)
        {
            return pages.ToList();
        }

        var picked = new List<PageContent>(HeadPages + TailPages + MiddleSamples);
        // 头 HeadPages
        picked.AddRange(pages.Take(HeadPages));

        // 尾 TailPages
        picked.AddRange(pages.TakeLast(TailPages));

        // 中间均匀采样
        var middleStart = HeadPages;
        var middleEnd = pages.Count - TailPages;
        var middleCount = middleEnd - middleStart;
        if (middleCount > 0)
        {
            var step = (double)middleCount / MiddleSamples;
            for (int i = 0; i < MiddleSamples; i++)
            {
                var idx = middleStart + (int)Math.Round(i * step);
                if (idx < middleEnd && !picked.Contains(pages[idx]))
                {
                    picked.Add(pages[idx]);
                }
            }
        }

        // 按页码排序
        return picked.OrderBy(p => p.PageNumber).ToList();
    }

    private static string BuildUserPrompt(Resource resource, string sampledText)
    {
        var metaLines = new List<string>
        {
            $"## 文档元信息",
            $"- 名称：{resource.Name}",
        };
        if (!string.IsNullOrWhiteSpace(resource.Description))
        {
            metaLines.Add($"- 描述：{resource.Description}");
        }
        if (!string.IsNullOrWhiteSpace(resource.FileExtension))
        {
            metaLines.Add($"- 格式：{resource.FileExtension}");
        }

        metaLines.Add("");
        metaLines.Add("## 文档内容（采样后）");
        metaLines.Add(sampledText);

        return string.Join("\n", metaLines);
    }

    private static (string Summary, string Keywords) ParseResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new UserFriendlyException("Qwen returned empty response");
        }

        // 剥离 markdown 代码块（如 ```json ... ``` 或 ``` ... ```）
        var cleaned = StripCodeFence(raw).Trim();

        // 直接尝试反序列化
        if (TryDeserialize(cleaned, out var summary, out var keywords))
        {
            return (NormalizeSummary(summary), NormalizeKeywords(keywords));
        }

        // 退而求其次：用正则提取 {...} 子串
        var match = Regex.Match(cleaned, @"\{[\s\S]*\}");
        if (match.Success)
        {
            var json = match.Value;
            if (TryDeserialize(json, out summary, out keywords))
            {
                return (NormalizeSummary(summary), NormalizeKeywords(keywords));
            }
        }

        throw new UserFriendlyException($"Failed to parse Qwen response as JSON: {Truncate(raw, 200)}");
    }

    private static string StripCodeFence(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.StartsWith("```"))
        {
            // 去掉首行 ```json 或 ```
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed.Substring(firstNewline + 1);
            }
            // 去掉尾部 ```
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
            {
                trimmed = trimmed.Substring(0, lastFence);
            }
        }
        return trimmed.Trim();
    }

    private static bool TryDeserialize(string json, out string summary, out string keywords)
    {
        summary = string.Empty;
        keywords = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String)
            {
                summary = s.GetString() ?? string.Empty;
            }
            if (root.TryGetProperty("keywords", out var k) && k.ValueKind == JsonValueKind.String)
            {
                keywords = k.GetString() ?? string.Empty;
            }
            return !string.IsNullOrWhiteSpace(summary);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }
        // 清理控制字符和多余空白
        summary = summary.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        summary = Regex.Replace(summary, @"\s+", " ").Trim();
        return summary;
    }

    private static string NormalizeKeywords(string keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return string.Empty;
        }

        // 全角逗号替换为半角
        keywords = keywords.Replace('，', ',').Replace('；', ',');

        var list = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => k.Length >= 1 && k.Length <= 20)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxKeywords)
            .ToList();

        return string.Join(",", list);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max);
}