using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.LearningAnalytics.Enums;
using KnowledgeHub.LearningAnalytics.Ocr;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 把 OCR 结果落库为「试卷 + 单题得分」，并尽力匹配应用内真实习题。
///
/// 匹配是**代码规则**而非模型判断：
/// 1) 题数与题库题数一致 → 顺序匹配；
/// 2) 不一致 → 顺序匹配但全部标记 <see cref="PaperMatchStatus.Low"/>，交给人工校对；
/// 3) 题干片段能在题库标题/题干中命中 → 提升该题匹配度；
/// 4) 分值对不上（>20% 差异）、识别不出分数 → 标记 Low。
/// 校对确认前不会写 <c>AnswerRecord</c>，因此不会污染掌握度矩阵。
/// </summary>
public class PaperIngestionService : ITransientDependency
{
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<PaperScript, Guid> _scriptRepository;
    private readonly IRepository<PaperItemScore, Guid> _itemRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IGuidGenerator _guidGenerator;

    public PaperIngestionService(
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<PaperScript, Guid> scriptRepository,
        IRepository<PaperItemScore, Guid> itemRepository,
        IIdentityUserRepository userRepository,
        IGuidGenerator guidGenerator)
    {
        _exerciseRepository = exerciseRepository;
        _chapterRepository = chapterRepository;
        _scriptRepository = scriptRepository;
        _itemRepository = itemRepository;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task<PaperScript> IngestAsync(
        Guid batchId,
        Guid courseId,
        string sourceFileName,
        PaperOcrResult result,
        Guid? studentId = null,
        string? studentName = null,
        string? studentNumber = null,
        CancellationToken cancellationToken = default)
    {
        // 教师上传时已指定学生 → 直接归属，不再依赖卷面学号识别
        var (resolvedStudentId, resolvedName, resolvedNumber) = studentId.HasValue
            ? (studentId, (studentName ?? string.Empty).Trim(), (studentNumber ?? string.Empty).Trim())
            : await ResolveStudentAsync(result);

        var script = new PaperScript(_guidGenerator.Create(), batchId, resolvedNumber, resolvedName)
        {
            StudentId = resolvedStudentId,
            OcrConfidence = Math.Round(result.Confidence, 3),
            OcrProvider = result.Provider,
            OcrModel = result.Model,
            SourceFileName = sourceFileName,
            RawOcrJson = Trim(result.RawJson, 8000),
            Confirmed = false,
        };

        var orderedExercises = await LoadOrderedExercisesAsync(courseId);
        var matches = MatchQuestions(result.Questions, orderedExercises);

        double total = 0;
        var index = 0;
        foreach (var match in matches)
        {
            index++;
            var questionNo = string.IsNullOrWhiteSpace(match.Question?.QuestionNo)
                ? index.ToString(CultureInfo.InvariantCulture)
                : match.Question!.QuestionNo.Trim();
            // 兜底：QuestionNo 列为 varchar(32)，模型偶发把题干塞进题号
            if (questionNo.Length > 32)
            {
                questionNo = index.ToString(CultureInfo.InvariantCulture);
            }

            var item = new PaperItemScore(
                _guidGenerator.Create(), script.Id, batchId, questionNo)
            {
                ExerciseId = match.Exercise?.Id,
                ChapterId = match.Exercise?.ChapterId,
                MatchStatus = match.Status,
                Score = match.Score,
                FullScore = match.FullScore,
                OcrConfidence = Math.Round(match.Confidence, 3),
                RawText = Trim(match.Question?.RawText, 512),
            };

            total += item.Score;
            await _itemRepository.InsertAsync(item, autoSave: true);
        }

        script.TotalScore = matches.Any()
            ? Math.Round(total, 1)
            : Math.Round(result.TotalScore ?? 0, 1);

        await _scriptRepository.InsertAsync(script, autoSave: true);
        return script;
    }

    // ══════════════════════════════════════════════════════
    //  学生匹配
    // ══════════════════════════════════════════════════════

    private async Task<(Guid? StudentId, string StudentName, string StudentNumber)> ResolveStudentAsync(PaperOcrResult result)
    {
        var number = (result.StudentNumber ?? string.Empty).Trim();
        var name = (result.StudentName ?? string.Empty).Trim();

        IdentityUser? user = null;

        if (!string.IsNullOrWhiteSpace(number))
        {
            var byName = await _userRepository.GetListAsync(userName: number, maxResultCount: 20);
            user = byName.FirstOrDefault(u => string.Equals(u.UserName, number, StringComparison.OrdinalIgnoreCase))
                   ?? byName.FirstOrDefault();
        }

        if (user == null && !string.IsNullOrWhiteSpace(name))
        {
            var byRealName = await _userRepository.GetListAsync(name: name, maxResultCount: 20);
            user = byRealName.FirstOrDefault(u =>
                       string.Equals($"{u.Name}{u.Surname}".Trim(), name, StringComparison.OrdinalIgnoreCase))
                   ?? (byRealName.Count == 1 ? byRealName[0] : null);
        }

        if (user == null && !string.IsNullOrWhiteSpace(name))
        {
            var byFilter = await _userRepository.GetListAsync(filter: name, maxResultCount: 20);
            user = byFilter.FirstOrDefault(u =>
                string.Equals($"{u.Name}{u.Surname}".Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        if (user != null)
        {
            var display = $"{user.Name}{user.Surname}".Trim();
            return (user.Id,
                string.IsNullOrWhiteSpace(display) ? (user.UserName ?? name) : display,
                string.IsNullOrWhiteSpace(number) ? (user.UserName ?? string.Empty) : number);
        }

        return (null,
            string.IsNullOrWhiteSpace(name) ? $"未识别学生 {DateTime.Now:HHmmss}" : name,
            string.IsNullOrWhiteSpace(number) ? string.Empty : number);
    }

    // ══════════════════════════════════════════════════════
    //  题目匹配
    // ══════════════════════════════════════════════════════

    private record MatchCandidate(
        PaperOcrQuestion? Question,
        Exercise? Exercise,
        PaperMatchStatus Status,
        double Score,
        double FullScore,
        double Confidence);

    private async Task<List<Exercise>> LoadOrderedExercisesAsync(Guid courseId)
    {
        var exercises = await _exerciseRepository.GetListAsync(e => e.CourseId == courseId);
        if (!exercises.Any())
        {
            return new List<Exercise>();
        }

        var chapterIds = exercises.Where(e => e.ChapterId.HasValue)
            .Select(e => e.ChapterId!.Value).Distinct().ToList();
        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.SortOrder)
            : new Dictionary<Guid, int>();

        return exercises
            .OrderBy(e => e.ChapterId.HasValue ? chapters.GetValueOrDefault(e.ChapterId.Value, int.MaxValue) : int.MaxValue)
            .ThenBy(e => e.CreationTime)
            .ThenBy(e => e.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static List<MatchCandidate> MatchQuestions(
        List<PaperOcrQuestion> questions,
        List<Exercise> exercises)
    {
        var result = new List<MatchCandidate>();

        // 模型一条题目都没给出（例如输出不是 JSON）：按题库顺序生成「待填分数」行，方便老师手工录入
        if (!questions.Any())
        {
            foreach (var exercise in exercises)
            {
                result.Add(new MatchCandidate(
                    null,
                    exercise,
                    PaperMatchStatus.Low,
                    0,
                    FullScoreOf(exercise),
                    0.15));
            }

            return result;
        }

        var countMatched = questions.Count == exercises.Count;

        // 题数与题库不一致时（试卷只考了题库的一部分），用「满分序列」在题库里滑窗对齐，
        // 对齐得分高则视为可信匹配（避免整卷都落进人工校对）。
        var aligned = countMatched ? null : AlignByFullScoreSignature(questions, exercises);

        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            var exercise = ResolveExercise(question, exercises, i, countMatched, aligned);

            var fullScore = question.FullScore ?? (exercise != null ? FullScoreOf(exercise) : 5.0);
            var score = question.Score ?? 0;
            var status = PaperMatchStatus.Matched;
            var confidence = 0.85;

            if (exercise == null)
            {
                status = PaperMatchStatus.None;
                confidence = 0.3;
            }
            else
            {
                if (question.Score == null)
                {
                    status = PaperMatchStatus.Low;
                    confidence = 0.45;
                }

                if (!countMatched)
                {
                    // 序列对齐命中则算「已匹配」，否则仍需人工确认
                    if (aligned != null)
                    {
                        confidence = Math.Min(confidence, 0.8);
                    }
                    else
                    {
                        status = PaperMatchStatus.Low;
                        confidence = Math.Min(confidence, 0.55);
                    }
                }

                // 分值差异过大：说明题目顺序或映射对不上
                if (question.FullScore.HasValue && exercise.Score > 0)
                {
                    var expected = exercise.Score;
                    var diff = Math.Abs(question.FullScore.Value - expected) / Math.Max(1.0, expected);
                    if (diff > 0.2)
                    {
                        status = status == PaperMatchStatus.None ? status : PaperMatchStatus.Low;
                        confidence = Math.Min(confidence, 0.5);
                    }
                }
            }

            result.Add(new MatchCandidate(
                question,
                exercise,
                status,
                Math.Clamp(score, 0, fullScore),
                fullScore,
                confidence));
        }

        return result;
    }

    private static Exercise? ResolveExercise(
        PaperOcrQuestion question,
        List<Exercise> exercises,
        int index,
        bool countMatched,
        List<Exercise>? aligned)
    {
        // 1) 题干片段命中：最强的文本证据
        if (!string.IsNullOrWhiteSpace(question.RawText) && question.RawText!.Length >= 4)
        {
            var probe = question.RawText.Trim();
            var byText = exercises.FirstOrDefault(e =>
                e.Title.Contains(probe, StringComparison.OrdinalIgnoreCase) ||
                e.QuestionContent.Contains(probe, StringComparison.OrdinalIgnoreCase));
            if (byText != null)
            {
                return byText;
            }
        }

        // 2) 滑窗对齐结果
        if (aligned != null && index < aligned.Count)
        {
            return aligned[index];
        }

        // 3) 顺序兜底（题数一致时可信，否则会标记待复核）
        return index < exercises.Count ? exercises[index] : null;
    }

    /// <summary>
    /// 用满分序列在题库里滑窗对齐：返回与识别题目一一对应的题库习题，匹配率不足时返回 null。
    /// 例如识别到 5/5/8/8/10/12 分，而题库有 20 题，可在其中找到与之吻合的 6 题窗口。
    /// </summary>
    private static List<Exercise>? AlignByFullScoreSignature(
        List<PaperOcrQuestion> questions,
        List<Exercise> exercises)
    {
        if (!questions.Any() || exercises.Count < questions.Count)
        {
            return null;
        }

        var detected = questions.Select(q => q.FullScore).ToList();
        var withScore = detected.Count(v => v.HasValue);
        if (withScore < Math.Max(2, detected.Count / 2))
        {
            // 满分识别太少，序列比对不可靠
            return null;
        }

        var bestWindow = -1;
        var bestScore = -1.0;

        for (var start = 0; start + questions.Count <= exercises.Count; start++)
        {
            var matches = 0;
            for (var i = 0; i < questions.Count; i++)
            {
                var expected = detected[i];
                if (!expected.HasValue)
                {
                    continue;
                }

                var actual = FullScoreOf(exercises[start + i]);
                if (Math.Abs(actual - expected.Value) <= Math.Max(1.0, expected.Value * 0.25))
                {
                    matches++;
                }
            }

            var ratio = (double)matches / Math.Max(1, withScore);
            if (ratio > bestScore)
            {
                bestScore = ratio;
                bestWindow = start;
            }
        }

        if (bestWindow < 0 || bestScore < 0.6)
        {
            return null;
        }

        return exercises.GetRange(bestWindow, questions.Count);
    }

    private static double FullScoreOf(Exercise exercise) => exercise.Score > 0 ? exercise.Score : 5.0;

    private static string? Trim(string? text, int max) =>
        string.IsNullOrEmpty(text) ? text : text.Length <= max ? text : text[..max];
}
