using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics.Ocr;

/// <summary>
/// Mock OCR：未配置 Qwen Key（或显式 `LearningAnalytics:OcrProvider=mock`）时使用。
/// 从文件名推断学号/姓名，生成确定性但带噪声的题目得分，用于演示「上传→校对→分析」全链路。
/// **结果不是真实成绩**，UI 会展示「Mock OCR」标记。
/// </summary>
public class MockPaperOcrRecognizer : IPaperOcrRecognizer, ITransientDependency
{
    private static readonly int[] DefaultFullScores = { 5, 5, 5, 8, 8, 10, 10, 12, 12, 15 };

    public string Provider => "mock";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<PaperOcrResult> RecognizeAsync(PaperOcrRequest request, CancellationToken cancellationToken = default)
    {
        var stem = Path.GetFileNameWithoutExtension(request.FileName ?? string.Empty);
        var numbers = Regex.Match(stem, @"\d{6,}");
        var studentNumber = numbers.Success ? numbers.Value : $"MOCK{Math.Abs(StableHash(stem)) % 100000:D5}";

        var nameMatch = Regex.Match(stem, @"[\u4e00-\u9fa5]{2,4}");
        var studentName = nameMatch.Success ? nameMatch.Value : stem;

        var seed = StableHash($"{stem}|{request.Content?.Length ?? 0}");
        var random = new Random(seed);
        var ability = 0.35 + random.NextDouble() * 0.55;

        var questions = new List<PaperOcrQuestion>();
        double total = 0;

        for (var i = 0; i < DefaultFullScores.Length; i++)
        {
            var fullScore = DefaultFullScores[i];
            var noise = (random.NextDouble() - 0.5) * 0.5;
            var ratio = Math.Clamp(ability + noise, 0, 1);
            var score = Math.Round(fullScore * ratio * 2, MidpointRounding.AwayFromZero) / 2;

            // 少量题目模拟「识别不出手写分数」，驱动人工校对流程
            double? readScore = random.NextDouble() < 0.12 ? null : score;
            total += readScore ?? 0;

            questions.Add(new PaperOcrQuestion(
                (i + 1).ToString(CultureInfo.InvariantCulture),
                readScore,
                fullScore,
                $"Mock 题目 {i + 1}"));
        }

        var confidence = Math.Round(0.55 + random.NextDouble() * 0.35, 3);

        return Task.FromResult(new PaperOcrResult(
            studentNumber,
            studentName,
            Math.Round(total, 1),
            questions,
            confidence,
            "{\"mock\":true}",
            Provider,
            "mock-v1"));
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in value)
            {
                hash = hash * 31 + c;
            }

            return hash & 0x7FFFFFFF;
        }
    }
}
