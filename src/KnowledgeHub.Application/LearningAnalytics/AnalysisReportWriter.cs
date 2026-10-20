using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics;

public record WeakPointText(string Name, string Reason, string Advice);

/// <summary>学生报告文字（数字来自代码事实，文字来自 LLM 或规则）。</summary>
public record StudentReportText(
    string Summary,
    List<WeakPointText> WeakPoints,
    List<string> StudyPlan,
    bool UsedLlm);

/// <summary>章节教学建议文字。</summary>
public record TeachingAdviceText(string ChapterTitle, string Advice, bool UsedLlm);

/// <summary>
/// 报告文字生成器。
///
/// 分工原则：**数字全部由代码计算**，LLM 只把结构化事实写成文字。
/// 若模型未配置 / 调用失败 / 输出校验不通过，一律回退到规则模板，保证报告不空。
/// </summary>
public class AnalysisReportWriter : ITransientDependency
{
    private const string SystemPrompt =
        "你是职业院校的教学分析助手。只能使用输入 facts 中出现的数字和知识点名称，" +
        "不得编造任何事实或新增知识点。语气具体、可执行、鼓励为主。";

    private readonly IConfiguration _configuration;
    private readonly ILogger<AnalysisReportWriter> _logger;

    public AnalysisReportWriter(IConfiguration configuration, ILogger<AnalysisReportWriter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════
    //  学生报告
    // ══════════════════════════════════════════════════════

    public async Task<StudentReportText> WriteStudentAsync(
        string courseName,
        double? score,
        double? classAverage,
        IReadOnlyList<WeakPointTextSource> weakPoints,
        bool allowLlm = true,
        CancellationToken cancellationToken = default)
    {
        var facts = new
        {
            course = courseName,
            score,
            classAverage,
            weakPoints = weakPoints.Select(w => new
            {
                name = w.Name,
                masteryPercent = (int)Math.Round(w.Mastery * 100),
                chapter = w.ChapterTitle,
            }),
        };

        var factsJson = JsonSerializer.Serialize(facts);
        var userPrompt =
            "facts: " + factsJson + "\n" +
            "请输出 JSON：{\"summary\":\"一句话总结\",\"weakPoints\":[{\"name\":\"知识点名（必须来自 facts）\"," +
            "\"reason\":\"失分原因\",\"advice\":\"具体复习动作\"}],\"studyPlan\":[\"按顺序的学习动作\"]}。只输出 JSON。";

        if (!allowLlm)
        {
            return RuleStudent(courseName, score, classAverage, weakPoints);
        }

        var raw = await CompleteAsync(userPrompt, cancellationToken);
        var parsed = TryParseStudent(raw, weakPoints.Select(w => w.Name).ToHashSet());
        if (parsed != null)
        {
            return parsed with { UsedLlm = true };
        }

        return RuleStudent(courseName, score, classAverage, weakPoints);
    }

    public record WeakPointTextSource(string Name, double Mastery, string? ChapterTitle);

    private static StudentReportText? TryParseStudent(string? raw, HashSet<string> allowedNames)
    {
        var json = ExtractJsonObject(raw);
        if (json == null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            var weak = new List<WeakPointText>();

            if (root.TryGetProperty("weakPoints", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || !allowedNames.Contains(name!))
                    {
                        // 输出里出现事实之外的知识点 → 丢弃重试（此处直接判定失败）
                        continue;
                    }

                    weak.Add(new WeakPointText(
                        name!,
                        item.TryGetProperty("reason", out var r) ? r.GetString() ?? string.Empty : string.Empty,
                        item.TryGetProperty("advice", out var a) ? a.GetString() ?? string.Empty : string.Empty));
                }
            }

            var plan = new List<string>();
            if (root.TryGetProperty("studyPlan", out var pArr) && pArr.ValueKind == JsonValueKind.Array)
            {
                plan.AddRange(pArr.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!));
            }

            if (string.IsNullOrWhiteSpace(summary) || !weak.Any())
            {
                return null;
            }

            return new StudentReportText(summary, weak, plan, true);
        }
        catch
        {
            return null;
        }
    }

    private static StudentReportText RuleStudent(
        string courseName,
        double? score,
        double? classAverage,
        IReadOnlyList<WeakPointTextSource> weakPoints)
    {
        var ordered = weakPoints.OrderBy(w => w.Mastery).ToList();
        var names = ordered.Select(w => w.Name).Take(3).ToList();

        var summary = new StringBuilder();
        summary.Append($"《{courseName}》当前");
        if (score.HasValue)
        {
            summary.Append($"得分 {score:0.#}");
            if (classAverage.HasValue)
            {
                summary.Append($"（班级平均 {classAverage:0.#}）");
            }
        }

        summary.Append($"存在 {ordered.Count} 个待加强知识点");
        if (names.Any())
        {
            summary.Append($"，优先复习「{string.Join("」「", names)}」");
        }

        summary.Append('。');

        var weak = ordered.Select(w => new WeakPointText(
            w.Name,
            $"掌握率 {(int)Math.Round(w.Mastery * 100)}%，低于学习目标",
            string.IsNullOrWhiteSpace(w.ChapterTitle)
                ? $"完成「{w.Name}」专项练习，再回看对应讲解。"
                : $"回看「{w.ChapterTitle}」中{w.Name}部分，并完成专项练习。"))
            .ToList();

        var plan = ordered.Take(4)
            .Select((w, i) => $"{i + 1}. 先复习「{w.Name}」（掌握率 {(int)Math.Round(w.Mastery * 100)}%），再做 5 道同类题")
            .ToList();

        return new StudentReportText(summary.ToString(), weak, plan, false);
    }

    // ══════════════════════════════════════════════════════
    //  教学建议
    // ══════════════════════════════════════════════════════

    public async Task<List<TeachingAdviceText>> WriteTeachingAsync(
        string courseName,
        IReadOnlyList<TeachingChapterFact> chapters,
        CancellationToken cancellationToken = default)
    {
        if (!chapters.Any())
        {
            return new List<TeachingAdviceText>();
        }

        var facts = new
        {
            course = courseName,
            chapters = chapters.Select(c => new
            {
                title = c.Title,
                ratePercent = (int)Math.Round(c.Rate * 100),
                topQuartilePercent = (int)Math.Round(c.TopQuartile * 100),
                stdDev = Math.Round(c.StdDev, 3),
                diagnosis = c.Diagnosis,
            }),
        };

        var userPrompt =
            "facts: " + JsonSerializer.Serialize(facts) + "\n" +
            "针对每个章节给出一条教学改进建议。输出 JSON 数组：[{\"title\":\"章节名（必须来自 facts）\",\"advice\":\"建议\"}]。只输出 JSON。";

        var raw = await CompleteAsync(userPrompt, cancellationToken);
        var parsed = TryParseTeaching(raw, chapters.Select(c => c.Title).ToHashSet());
        if (parsed != null && parsed.Count > 0)
        {
            return parsed;
        }

        return chapters.Select(c => new TeachingAdviceText(c.Title, RuleTeachingAdvice(c), false)).ToList();
    }

    public record TeachingChapterFact(string Title, double Rate, double TopQuartile, double StdDev, string Diagnosis);

    private static List<TeachingAdviceText>? TryParseTeaching(string? raw, HashSet<string> allowedTitles)
    {
        var json = ExtractJsonArray(raw);
        if (json == null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new List<TeachingAdviceText>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
                var advice = item.TryGetProperty("advice", out var a) ? a.GetString() : null;
                if (string.IsNullOrWhiteSpace(title) || !allowedTitles.Contains(title!))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(advice))
                {
                    continue;
                }

                result.Add(new TeachingAdviceText(title!, advice!, true));
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    public static string RuleTeachingAdvice(TeachingChapterFact c)
    {
        var rate = (int)Math.Round(c.Rate * 100);
        var top = (int)Math.Round(c.TopQuartile * 100);

        return c.Diagnosis switch
        {
            "gap" =>
                $"得分率 {rate}%，高分学生也仅 {top}% → 倾向讲解不足，建议就本节关键方法做一次对比讲解，并增加 2 课时针对性练习。",
            "polarized" =>
                $"得分率 {rate}%，两极分化明显（标准差 {c.StdDev:0.00}）→ 建议分层布置课堂练习，为后 25% 学生补充基础模板题。",
            "good" =>
                $"得分率 {rate}%，达成度高，可作为经验沉淀，复用到后续章节的教学设计。",
            _ => $"得分率 {rate}%，接近目标 → 建议针对失分集中的题型做一次课堂讲评。",
        };
    }

    // ══════════════════════════════════════════════════════
    //  LLM 调用
    // ══════════════════════════════════════════════════════

    private async Task<string?> CompleteAsync(string userPrompt, CancellationToken cancellationToken)
    {
        try
        {
            var model = _configuration["Qwen:Model"] ?? "qwen-flash";
            var chatClient = await QwenClient.CreateChatClient(_configuration, model);

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            };
            var options = new ChatOptions { Temperature = 0.3f, MaxOutputTokens = 1024 };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "智学分析报告 LLM 生成失败，回退规则模板");
            return null;
        }
    }

    private static string? ExtractJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().Trim('`');
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : null;
    }

    private static string? ExtractJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().Trim('`');
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : null;
    }
}
