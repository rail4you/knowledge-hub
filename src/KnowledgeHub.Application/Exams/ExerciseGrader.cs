using System;
using System.Linq;
using KnowledgeHub.Exams.Enums;

namespace KnowledgeHub.Exams;

/// <summary>
/// 习题自动判分（客观题）。
/// <para>
/// 与 <c>StudentExerciseRecordAppService</c> 里的私有实现保持一致的口径：
/// 单选比字母、多选比集合、判断归一化 true/false（兼容 对/错/√/×）、填空 trim + 忽略大小写；
/// 主观题一律返回 null（不自动判）。
/// </para>
/// <para>
/// 抽成公共类是为了让课堂练习（<c>ClassroomPracticeAppService</c>）复用同一套规则，
/// 避免出现"平时练习判对了、课堂练习判错"的两套逻辑。
/// </para>
/// </summary>
public static class ExerciseGrader
{
    public static bool? Grade(ExerciseType type, string? correctAnswer, string? studentAnswer)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(correctAnswer))
        {
            return null;
        }

        return type switch
        {
            ExerciseType.SingleChoice => string.Equals(
                NormalizeChoiceToken(studentAnswer),
                NormalizeChoiceToken(correctAnswer),
                StringComparison.OrdinalIgnoreCase),
            ExerciseType.MultiChoice => AreSetsEqual(studentAnswer, correctAnswer),
            ExerciseType.TrueFalse => GradeTrueFalse(studentAnswer, correctAnswer),
            ExerciseType.FillBlank => string.Equals(
                studentAnswer.Trim(),
                correctAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase),
            _ => null
        };
    }

    /// <summary>是否是客观题（可自动判分）。</summary>
    public static bool IsObjective(ExerciseType type) =>
        type is ExerciseType.SingleChoice
            or ExerciseType.MultiChoice
            or ExerciseType.TrueFalse
            or ExerciseType.FillBlank;

    /// <summary>多选答案归一化：拆成去重排序后的字母集合再比较（"A,B" == "b, a"）。</summary>
    private static bool AreSetsEqual(string a, string b)
    {
        static string[] Split(string s) => s
            .Split(new[] { ',', '，', ';', '；', '、', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => NormalizeChoiceToken(x).ToUpperInvariant())
            .Where(x => x.Length > 0)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var left = Split(a);
        var right = Split(b);
        return left.Length == right.Length && left.SequenceEqual(right);
    }

    private static string NormalizeChoiceToken(string raw)
    {
        var t = raw.Trim();
        // 支持 "A" / "A." / "A、" / "(A)" / "选项A" 这类写法
        t = t.Trim('(', ')', '（', '）', '.', '。', '、', ',', '，', ' ');
        if (t.StartsWith("选项", StringComparison.Ordinal))
        {
            t = t.Substring(2).Trim();
        }

        return t;
    }

    private static bool GradeTrueFalse(string studentAnswer, string correctAnswer)
    {
        var s = NormalizeTrueFalse(studentAnswer);
        var c = NormalizeTrueFalse(correctAnswer);
        if (s.HasValue && c.HasValue)
        {
            return s.Value == c.Value;
        }

        return string.Equals(studentAnswer.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool? NormalizeTrueFalse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var t = raw.Trim().ToLowerInvariant().Trim('\'', '"', '。', '.', '!', '！');
        return t switch
        {
            "true" or "t" or "1" or "yes" or "y" or "√" or "✓" or "对" or "正确" or "是" or "真" or "right" => true,
            "false" or "f" or "0" or "no" or "n" or "×" or "x" or "错" or "错误" or "否" or "假" or "wrong" => false,
            _ => null
        };
    }
}
