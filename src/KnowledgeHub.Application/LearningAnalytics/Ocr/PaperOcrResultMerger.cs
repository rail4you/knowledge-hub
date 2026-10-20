using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace KnowledgeHub.LearningAnalytics.Ocr;

/// <summary>
/// 多页试卷（PDF 拆页 / 多张照片属于同一份试卷）的 OCR 结果合并：
/// 学生信息取第一个非空值，题目按页拼接（题号冲突时加页码前缀），置信度取均值。
/// </summary>
public static class PaperOcrResultMerger
{
    public static PaperOcrResult Merge(IReadOnlyList<PaperOcrResult> results)
    {
        if (results.Count == 0)
        {
            throw new ArgumentException("没有可合并的 OCR 结果", nameof(results));
        }

        if (results.Count == 1)
        {
            return results[0];
        }

        var studentNumber = results
            .Select(r => r.StudentNumber)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var studentName = results
            .Select(r => r.StudentName)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        // 跨页重复出现的题号（每页从 1 开始编号）→ 加页码前缀，避免校对界面出现重复行号
        var duplicated = results
            .SelectMany(r => r.Questions.Select(q => q.QuestionNo))
            .GroupBy(no => no, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var questions = new List<PaperOcrQuestion>();
        for (var page = 0; page < results.Count; page++)
        {
            foreach (var question in results[page].Questions)
            {
                var no = duplicated.Contains(question.QuestionNo)
                    ? $"{page + 1}.{question.QuestionNo}"
                    : question.QuestionNo;
                questions.Add(question with { QuestionNo = no });
            }
        }

        double? totalScore = questions.Any(q => q.Score.HasValue)
            ? questions.Where(q => q.Score.HasValue).Sum(q => q.Score!.Value)
            : results.Where(r => r.TotalScore.HasValue).Select(r => r.TotalScore!.Value).DefaultIfEmpty().Sum() switch
            {
                0 => null,
                var sum => sum,
            };

        var confidence = Math.Round(results.Average(r => r.Confidence), 3);

        var rawJson = JsonSerializer.Serialize(new
        {
            merged = true,
            pages = results.Select((r, i) => new { page = i + 1, provider = r.Provider, model = r.Model, raw = r.RawJson }),
        });

        return new PaperOcrResult(
            studentNumber?.Trim(),
            studentName?.Trim(),
            totalScore,
            questions,
            confidence,
            rawJson,
            results[0].Provider,
            results[0].Model);
    }
}
