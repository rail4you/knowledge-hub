using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeHub.Courses;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>一条自动映射建议。</summary>
public record KnowledgeResourceMatch(
    Guid KnowledgeResourceId,
    string KnowledgeResourceName,
    double Score,
    string Reason);

/// <summary>
/// 「智学分析知识点（KnowledgePoint）」→「课程知识点树（KnowledgeResource）」的自动匹配。
///
/// <para>
/// 背景：两套知识点体系原本互不相通 —— 前者来自试卷/习题抽取，后者是课程内容主干。
/// B1 已给出 <c>KnowledgePoint.KnowledgeResourceId</c>（可空）作为连接点，
/// 本类负责把「同章节 + 名称相近」的点自动接上，灰区留给老师在治理页人工确认。
/// </para>
///
/// <para>
/// 匹配策略（按优先级，命中即停）：
/// ① 同章节 **归一化名称完全相等** → 0.98；
/// ② 同名（忽略章节）→ 0.90；
/// ③ 同章节 **互相包含**（短的是长的子串，且长度差 ≤ 4 字）→ 0.80；
/// ④ 同章节 **字符 Jaccard ≥ 0.75** → 0.70；
/// ⑤ （可选，includeFuzzy）跨章节 Jaccard ≥ 0.85 → 0.60。
/// </para>
/// 只有 ≥ <see cref="AutoLinkThreshold"/> 才由「自动映射」直接写库，其余进灰区。
/// </summary>
public static class KnowledgeResourceMapper
{
    /// <summary>自动写库的置信度门槛（低于它的只作为建议返回，等人工确认）。</summary>
    public const double AutoLinkThreshold = 0.80;

    /// <summary>归一化：去空白、去包裹引号/括号、全角转半角常见符号、忽略大小写。</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var s = raw.Trim();

        // 去掉常见的"单元/知识项/知识点"后缀噪音（老师命名习惯不一致）
        foreach (var suffix in new[] { "知识点", "知识项", "知识元", "单元", "模块" })
        {
            if (s.Length > suffix.Length && s.EndsWith(suffix, StringComparison.Ordinal))
            {
                s = s[..^suffix.Length].Trim();
                break;
            }
        }

        s = s.Trim('「', '」', '"', '\'', '“', '”', '(', ')', '（', '）', '【', '】', '[', ']');
        s = s.Replace(" ", string.Empty)
             .Replace("\u3000", string.Empty)   // 全角空格
             .Replace("　", string.Empty)
             .Replace("—", "-")
             .Replace("－", "-")
             .Replace("：", ":")
             .Replace("，", ",")
             .Replace("、", ",")
             .Replace("。", ".");

        return s.ToLowerInvariant();
    }

    /// <summary>
    /// 为一批 KP 找出候选知识点。返回 kpId → 最佳匹配。
    /// <paramref name="includeFuzzy"/> 为真时额外给出跨章节的模糊匹配（低置信，仅建议）。
    /// </summary>
    public static Dictionary<Guid, KnowledgeResourceMatch> Match(
        IReadOnlyCollection<KnowledgePoint> knowledgePoints,
        IReadOnlyCollection<KnowledgeResource> resources,
        bool includeFuzzy = false)
    {
        var result = new Dictionary<Guid, KnowledgeResourceMatch>();
        if (knowledgePoints.Count == 0 || resources.Count == 0)
        {
            return result;
        }

        // 预归一化，避免 O(n²) 里反复字符串处理
        var normalized = resources
            .Select(r => new
            {
                Node = r,
                Key = Normalize(r.Name),
                r.ChapterId
            })
            .Where(x => x.Key.Length > 0)
            .ToList();

        var byChapter = normalized
            .Where(x => x.ChapterId.HasValue)
            .GroupBy(x => x.ChapterId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var all = normalized;

        foreach (var kp in knowledgePoints)
        {
            var key = Normalize(kp.Name);
            if (key.Length == 0)
            {
                continue;
            }

            var chapterPool = kp.ChapterId.HasValue && byChapter.TryGetValue(kp.ChapterId.Value, out var pool)
                ? pool
                : new();

            // ① 同章节完全相等
            var hit = chapterPool.FirstOrDefault(x => x.Key == key);
            if (hit != null)
            {
                result[kp.Id] = new KnowledgeResourceMatch(hit.Node.Id, hit.Node.Name, 0.98, "同章节同名");
                continue;
            }

            // ② 跨章节同名
            hit = all.FirstOrDefault(x => x.Key == key);
            if (hit != null)
            {
                result[kp.Id] = new KnowledgeResourceMatch(hit.Node.Id, hit.Node.Name, 0.90, "同名（不同章节）");
                continue;
            }

            // ③ 同章节互相包含
            var contains = chapterPool
                .Where(x => x.Key.Length >= 2 && key.Length >= 2 &&
                            (x.Key.Contains(key, StringComparison.Ordinal) || key.Contains(x.Key, StringComparison.Ordinal)))
                .Where(x => Math.Abs(x.Key.Length - key.Length) <= 4)
                .OrderByDescending(x => x.Key.Length)
                .FirstOrDefault();
            if (contains != null)
            {
                result[kp.Id] = new KnowledgeResourceMatch(
                    contains.Node.Id, contains.Node.Name, 0.80, "同章节名称包含");
                continue;
            }

            // ④ 同章节字符相似
            var similar = chapterPool
                .Select(x => new { x.Node, Score = KpGenerationJob.CharJaccard(key, x.Key) })
                .Where(x => x.Score >= 0.75)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();
            if (similar != null)
            {
                result[kp.Id] = new KnowledgeResourceMatch(
                    similar.Node.Id, similar.Node.Name, 0.70, $"同章节相似 {similar.Score:P0}");
                continue;
            }

            // ⑤ 跨章节模糊（仅建议，不自动写库）
            if (!includeFuzzy)
            {
                continue;
            }

            var fuzzy = all
                .Select(x => new { x.Node, Score = KpGenerationJob.CharJaccard(key, x.Key) })
                .Where(x => x.Score >= 0.85)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();
            if (fuzzy != null)
            {
                result[kp.Id] = new KnowledgeResourceMatch(
                    fuzzy.Node.Id, fuzzy.Node.Name, 0.60, $"跨章节相似 {fuzzy.Score:P0}");
            }
        }

        return result;
    }
}
