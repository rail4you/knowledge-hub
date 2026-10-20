using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics;

public record ExtractedKnowledgePoint(string Name, double Weight);

/// <summary>从章节抽取出的知识点（带简短描述）</summary>
public record ExtractedChapterKnowledgePoint(string Name, string? Description);

/// <summary>
/// 用 LLM 从习题题干抽取 1~3 个知识点（带权重）。
/// 提示词会带上课程已有知识点名，显著减少「同义不同词」。
/// 失败时返回空列表（调用方保持原映射，不会破坏已有数据）。
/// </summary>
public class KnowledgePointExtractor : ITransientDependency
{
    private const string SystemPrompt =
        "你是课程知识点标注助手。请从题目中抽取 1~3 个考察的知识点，输出严格 JSON 数组，不要解释。";

    private readonly IConfiguration _configuration;
    private readonly ILogger<KnowledgePointExtractor> _logger;

    public KnowledgePointExtractor(IConfiguration configuration, ILogger<KnowledgePointExtractor> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<ExtractedKnowledgePoint>> ExtractAsync(
        string courseName,
        string title,
        string questionContent,
        string? options,
        IReadOnlyCollection<string> existingNames,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var model = _configuration["LearningAnalytics:KpExtractionModel"]
                        ?? _configuration["Qwen:Model"]
                        ?? "qwen-plus";
            var chatClient = await QwenClient.CreateChatClient(_configuration, model);

            var existing = existingNames.Count == 0
                ? "（本课程暂无已建知识点）"
                : string.Join("、", existingNames.Take(120));

            var userPrompt = $$"""
                课程：{{courseName}}
                已有知识点（优先复用，不要换说法重复造词）：{{existing}}

                题目：{{title}}
                题干：{{Truncate(questionContent, 1200)}}
                {{(string.IsNullOrWhiteSpace(options) ? string.Empty : $"选项：{Truncate(options, 600)}")}}

                要求：
                1) 抽取该题考查的 1~3 个知识点，名称尽量短（≤12 字），优先使用已有知识点名称；
                2) 多个知识点时给出权重，weight 之和为 1；
                3) 只输出 JSON 数组，例如：[{"name":"级数收敛判别","weight":0.7},{"name":"极限","weight":0.3}]
                """;

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            };
            var options2 = new ChatOptions { Temperature = 0.1f, MaxOutputTokens = 400 };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var response = await chatClient.GetResponseAsync(messages, options2, cts.Token);
            return Parse(response.Text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "知识点抽取失败：{Title}", title);
            return new List<ExtractedKnowledgePoint>();
        }
    }

    /// <summary>
    /// 用 LLM 从「章节标题 + 章节简介 + 章节资源标题」判断该章节应包含哪些知识点。
    /// 失败时返回空列表（调用方回退到「章节标题即知识点」的兜底策略）。
    /// </summary>
    public async Task<List<ExtractedChapterKnowledgePoint>> ExtractFromChapterAsync(
        string courseName,
        string chapterTitle,
        string? chapterDescription,
        IReadOnlyCollection<string> resourceTitles,
        IReadOnlyCollection<string> existingNames,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var model = _configuration["LearningAnalytics:KpExtractionModel"]
                        ?? _configuration["Qwen:Model"]
                        ?? "qwen-plus";
            var chatClient = await QwenClient.CreateChatClient(_configuration, model);

            var existing = existingNames.Count == 0
                ? "（本课程暂无已建知识点）"
                : string.Join("、", existingNames.Take(120));

            var resources = resourceTitles.Count == 0
                ? string.Empty
                : $"\n章节资源：{string.Join("、", resourceTitles.Take(20))}";

            var userPrompt = $$"""
                课程：{{courseName}}
                章节：{{chapterTitle}}
                章节简介：{{Truncate(chapterDescription, 400)}}
                {{resources}}
                课程已有知识点（优先复用，不要换说法重复造词）：{{existing}}

                要求：
                1) 判断该章节真正考察的知识点，给出 1~3 个，名称尽量短（≤12 字）；
                2) 优先复用已有知识点名称；确需新建时给出与已有知识点明显不同的名称；
                3) 每个知识点附一句不超过 30 字的说明；
                4) 只输出 JSON 数组，例如：[{"name":"级数收敛判别","description":"判断无穷级数是否收敛"}]
                """;

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            };
            var options2 = new ChatOptions { Temperature = 0.1f, MaxOutputTokens = 400 };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var response = await chatClient.GetResponseAsync(messages, options2, cts.Token);
            return ParseChapter(response.Text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "章节知识点抽取失败：{Chapter}", chapterTitle);
            return new List<ExtractedChapterKnowledgePoint>();
        }
    }

    /// <summary>解析章节知识点抽取结果（JSON 数组，元素含 name / description）。</summary>
    internal static List<ExtractedChapterKnowledgePoint> ParseChapter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<ExtractedChapterKnowledgePoint>();
        }

        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            var fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) text = text[..fence];
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return new List<ExtractedChapterKnowledgePoint>();
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var result = new List<ExtractedChapterKnowledgePoint>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var description = item.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString()?.Trim()
                    : null;

                result.Add(new ExtractedChapterKnowledgePoint(name!, string.IsNullOrWhiteSpace(description) ? null : description));
            }

            return result
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(3)
                .ToList();
        }
        catch (JsonException)
        {
            return new List<ExtractedChapterKnowledgePoint>();
        }
    }

    internal static List<ExtractedKnowledgePoint> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<ExtractedKnowledgePoint>();
        }

        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            var fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) text = text[..fence];
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return new List<ExtractedKnowledgePoint>();
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var result = new List<ExtractedKnowledgePoint>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var weight = item.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number
                    ? w.GetDouble()
                    : 1.0;

                result.Add(new ExtractedKnowledgePoint(name!, weight));
            }

            var distinct = result
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ExtractedKnowledgePoint(g.Key, g.Sum(x => x.Weight)))
                .Take(3)
                .ToList();

            var sum = distinct.Sum(d => d.Weight);
            if (sum <= 0)
            {
                return distinct.Select(d => d with { Weight = 1.0 / Math.Max(1, distinct.Count) }).ToList();
            }

            return distinct.Select(d => d with { Weight = Math.Round(d.Weight / sum, 4) }).ToList();
        }
        catch (JsonException)
        {
            return new List<ExtractedKnowledgePoint>();
        }
    }

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max];
}
