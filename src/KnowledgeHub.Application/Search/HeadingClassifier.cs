using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace KnowledgeHub.Application.Search;

/// <summary>
/// Classify docx paragraphs as headings or body text using multi-signal heuristics.
/// Priority: numeric prefix depth > Chinese chapter/section > style level fallback.
/// </summary>
public static class HeadingClassifier
{
    private static readonly Regex NumericPrefix =
        new(@"^\s*(\d+(?:\.\d+)*)(?:[\s.、]|$)", RegexOptions.Compiled);
    private static readonly Regex ChineseChapter =
        new(@"^\s*第[一二三四五六七八九十百千零\d]+章", RegexOptions.Compiled);
    private static readonly Regex ChineseSection =
        new(@"^\s*第[一二三四五六七八九十百千零\d]+节", RegexOptions.Compiled);

    public record Result(bool IsHeading, int Level, string? NumberPrefix = null);

    /// <summary>
    /// Classify a paragraph text as heading or body.
    /// Returns (IsHeading, Level) where Level is the heading depth (1=top, 2=sub, ...).
    /// </summary>
    public static Result Classify(string text, int styleLevel)
    {
        text = text.Trim();

        // Rule 1: Numeric prefix (1, 1.1, 1.1.1, etc.) — most reliable signal
        var m = NumericPrefix.Match(text);
        if (m.Success && !LooksLikeSentence(text))
        {
            int depth = m.Groups[1].Value.Count(c => c == '.') + 1;
            return new Result(true, depth, m.Groups[1].Value);
        }

        // Rule 2: Chinese chapter/section markers
        if (ChineseChapter.IsMatch(text)) return new Result(true, 1);
        if (ChineseSection.IsMatch(text)) return new Result(true, 2);

        // Rule 3: Short, no sentence ending, no "label: long text" pattern → likely heading
        if (!LooksLikeSentence(text) && text.Length <= 20)
            return new Result(true, styleLevel);

        // Rule 4: Everything else is body text
        return new Result(false, styleLevel);
    }

    private static bool LooksLikeSentence(string t)
    {
        // Ends with sentence terminal punctuation
        if (t.EndsWith('。') || t.EndsWith('！') || t.EndsWith('？') || t.EndsWith('.'))
            return true;

        // "Label: long description" pattern (colon within first 15 chars, 10+ chars after)
        int colonIdx = Math.Max(t.IndexOf('：'), t.IndexOf(':'));
        if (colonIdx > 0 && colonIdx < 15 && t.Length - colonIdx > 10)
            return true;

        // Long text (>30 chars) is likely body
        return t.Length > 30;
    }
}
