using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// 简单的中文文本分词与高频词提取工具。
/// 基于 n-gram + 停用词过滤，不依赖外部分词库。
/// </summary>
public static class ChineseTextTokenizer
{
    // 常见中文停用词（标点、助词、连词、介词等）
    private static readonly HashSet<char> ChineseStopChars =
    [
        '的', '了', '在', '是', '我', '有', '和', '就', '不', '人', '都', '一', '个',
        '上', '也', '很', '到', '说', '要', '去', '你', '会', '着', '没', '看', '好',
        '这', '他', '她', '它', '们', '那', '些', '与', '及', '或', '但',
        '而', '被', '把', '对', '从', '以', '为', '因', '所', '让', '比', '向',
        '能', '可', '得', '过', '下', '中', '大', '小', '多', '少', '如', '若',
        '虽', '然', '因', '果', '而', '且', '并', '其', '中', '于', '之', '乎',
        '者', '也', '已', '将', '又', '再', '还', '则', '当', '即', '便', '才',
        '么', '吗', '呢', '啊', '哦', '嗯', '哈', '呀', '嘛', '呐', '罢', '啦',
        '哪', '怎', '什', '么', '何', '谁', '几', '多', '少', '很', '太', '极',
        '更', '最', '愈', '越', '稍', '略', '颇', '几', '总', '共', '凡', '只',
        '唯', '单', '仅', '光', '独', '净', '都'
    ];

    // 不参与高频词提取的单字（用于过滤 n-gram 中的噪音）
    private static readonly HashSet<char> NoiseChars =
    [
        '的', '了', '在', '是', '我', '有', '和', '就', '不', '人', '都', '一', '个',
        '上', '也', '很', '到', '说', '要', '去', '你', '会', '着', '没', '看', '好',
        '这', '他', '她', '它', '们', '那', '些', '与', '及', '或', '但',
        '被', '把', '对', '从', '以', '为', '因', '所', '让', '比', '向',
        '能', '可', '得', '过', '下', '中', '大', '小', '多', '少', '如', '若',
        '于', '之', '乎', '者', '也', '已', '将', '又', '再', '还', '则', '才',
        '么', '吗', '呢', '啊', '哦', '嗯', '哈', '呀', '嘛', '呐', '罢', '啦',
        '哪', '怎', '什', '何', '谁', '几', '很', '太', '极',
        '更', '最', '愈', '越', '稍', '略', '颇', '几', '总', '共', '凡', '只',
        '唯', '单', '仅', '光', '独', '净',
        '—', '–', '·', '…', '○', '●', '◆', '□', '■', '△', '▲', '☆', '★', '※'
    ];

    /// <summary>
    /// 从文本中提取高频中文词汇。
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="maxWords">返回最大词数</param>
    /// <returns>按频率降序排列的 (词, 频率) 列表</returns>
    public static List<(string Word, int Frequency)> ExtractHotWords(string text, int maxWords = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // 提取所有可能的中文词汇候选（2-6 字组合）
        var candidates = ExtractCandidates(text);
        if (candidates.Count == 0)
        {
            // 回退：使用分词片段（按标点分割）
            candidates = ExtractFallbackSegments(text);
        }

        // 计数
        var freq = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (IsValidPhrase(candidate))
            {
                freq[candidate] = freq.GetValueOrDefault(candidate) + 1;
            }
        }

        // 排序取 top N
        return freq
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Length) // 同频词短的优先
            .Take(maxWords)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>
    /// 提取候选词：优先使用标点/空格分割出的片段，再从中提取 2-6 字 n-gram。
    /// </summary>
    private static List<string> ExtractCandidates(string text)
    {
        var candidates = new List<string>();

        // 按标点、空白、英文/数字等分割
        const string splitPattern = @"[，。！？、；：""""''\[\]【】（）\(\)——\-=+\.\,\!\?\;\:\s\d\w\u3000\u00A0]+";
        var segments = Regex.Split(text, splitPattern);

        foreach (var segment in segments)
        {
            var clean = segment.Trim();
            if (clean.Length < 2) continue;

            // 对长片段提取 2-6 字 n-gram
            var maxGram = Math.Min(6, clean.Length);
            for (var gramLen = 2; gramLen <= maxGram; gramLen++)
            {
                for (var i = 0; i <= clean.Length - gramLen; i++)
                {
                    var gram = clean.Substring(i, gramLen);
                    candidates.Add(gram);
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// 回退策略：当常规 n-gram 提取不到候选时，按粗粒度分割提取中文短语。
    /// </summary>
    private static List<string> ExtractFallbackSegments(string text)
    {
        var segments = new List<string>();

        // 提取连续中文序列（中间可能夹杂英文标点）
        var matches = Regex.Matches(text, @"[\u4e00-\u9fff]{2,}");
        foreach (Match match in matches)
        {
            var seg = match.Value;
            if (seg.Length >= 2)
            {
                segments.Add(seg);
            }
        }

        return segments;
    }

    /// <summary>
    /// 判断一个短语是否有效（不含噪音字、首尾不能是停用词、长度 >= 2）。
    /// </summary>
    private static bool IsValidPhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase) || phrase.Length < 2) return false;

        // 首尾不能是典型的停用词/噪音字
        if (ChineseStopChars.Contains(phrase[0]) || ChineseStopChars.Contains(phrase[^1]))
            return false;

        // 不能全部由噪音字组成
        var noiseCount = 0;
        foreach (var c in phrase)
        {
            if (NoiseChars.Contains(c))
                noiseCount++;
        }
        if (noiseCount == phrase.Length) return false;

        // 噪音字比例不能超过 50%
        if (noiseCount > phrase.Length / 2) return false;

        return true;
    }
}
