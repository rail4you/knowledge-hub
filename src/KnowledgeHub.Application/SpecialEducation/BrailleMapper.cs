using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnowledgeHub.Application.SpecialEducation;

/// <summary>
/// 盲文点位工具：Unicode 盲文（U+2800–U+28FF，公式 U+2800 + Σ2^(d-1)）与点位互转，
/// 以及英语一级盲文（Grade 1）ASCII 子集映射。中文现行盲文不做整句机翻，
/// 仅提供经人工核验的教学样本（见 SeedMockData），AI 生成内容一律标注待教师核对。
/// </summary>
public static class BrailleMapper
{
    private static readonly int[] BitByDot = { 0, 1, 2, 4, 8, 16, 32, 64, 128 };

    /// <summary>点位（1-6/8）→ Unicode 盲文字符。</summary>
    public static char DotsToChar(IEnumerable<int> dots)
    {
        var value = dots.Where(d => d >= 1 && d <= 8).Sum(d => BitByDot[d]);
        return (char)(0x2800 + value);
    }

    /// <summary>Unicode 盲文字符 → 点位列表。非盲文字符返回空。</summary>
    public static List<int> CharToDots(char c)
    {
        var result = new List<int>();
        if (c < '\u2800' || c > '\u28FF') return result;
        var offset = c - 0x2800;
        for (var d = 1; d <= 8; d++)
        {
            if ((offset & BitByDot[d]) != 0) result.Add(d);
        }
        return result;
    }

    public static string DotsToString(IEnumerable<int> dots) => string.Join("", dots);

    private static readonly Dictionary<char, string> Grade1 = new()
    {
        { 'a', "1" }, { 'b', "12" }, { 'c', "14" }, { 'd', "145" }, { 'e', "15" },
        { 'f', "124" }, { 'g', "1245" }, { 'h', "125" }, { 'i', "24" }, { 'j', "245" },
        { 'k', "13" }, { 'l', "123" }, { 'm', "134" }, { 'n', "1345" }, { 'o', "135" },
        { 'p', "1234" }, { 'q', "12345" }, { 'r', "1235" }, { 's', "234" }, { 't', "2345" },
        { 'u', "136" }, { 'v', "1236" }, { 'w', "2456" }, { 'x', "1346" }, { 'y', "13456" },
        { 'z', "1356" },
        { '.', "256" }, { ',', "2" }, { '?', "236" }, { '!', "235" }, { ':', "25" },
        { ';', "23" }, { '-', "36" },
    };

    private const string NumberSignDots = "3456";

    /// <summary>英语一级盲文：字母照表，数字前加数号⠼，空格用空方，其余字符原样保留。</summary>
    public static string ToGrade1English(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                sb.Append('⠀');
            }
            else if (ch >= '0' && ch <= '9')
            {
                sb.Append(DotsToChar(NumberSignDots.Select(c => c - '0')));
                var letter = ch == '0' ? 'j' : (char)('a' + ch - '1');
                sb.Append(DotsToChar(Grade1[letter].Select(c => c - '0')));
            }
            else if (Grade1.TryGetValue(char.ToLowerInvariant(ch), out var dots))
            {
                sb.Append(DotsToChar(dots.Select(c => c - '0')));
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }
}
