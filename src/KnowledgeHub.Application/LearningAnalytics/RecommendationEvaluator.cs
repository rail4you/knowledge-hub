using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.LearningAnalytics.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 推荐效果离线评估（留出法）。
///
/// 思路：把每个学生按时间排序的作答留出最近 <c>testSize</c> 条作为测试集，其余作为历史；
/// 只用历史计算掌握度、相似学生与协同信号（严格避免用测试集泄漏），
/// 再对候选题按三路权重打分取 top-K，统计：
/// - WeakHitRate@K：测试集里「学生答错的题」有多少被推荐命中（应越高越好）
/// - Precision@K：推荐 top-K 中确实是错题的比例（应越高越好，因只有测试集知道对错，属保守下界）
///
/// 对若干组权重分别计算，给出建议权重（按 WeakHitRate 优先、Precision 次之）。
/// </summary>
public class RecommendationEvaluator : ITransientDependency
{
    private static readonly (string Name, RecommendationWeights Weights)[] CandidateWeightSets =
    {
        ("当前配置", RecommendationWeights.Default),
        ("纯内容", new RecommendationWeights(1, 0, 0)),
        ("纯协同", new RecommendationWeights(0, 1, 0)),
        ("纯难度", new RecommendationWeights(0, 0, 1)),
        ("内容为主", new RecommendationWeights(0.6, 0.2, 0.2)),
        ("协同为主", new RecommendationWeights(0.3, 0.5, 0.2)),
        ("三路均衡", new RecommendationWeights(0.34, 0.33, 0.33)),
    };

    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ExerciseKnowledgePoint, Guid> _mapRepository;
    private readonly IRepository<AnswerRecord, Guid> _answerRepository;
    private readonly IRepository<StudentExerciseRecord> _onlineRepository;
    private readonly IConfiguration _configuration;
    private readonly RecommendationWeightProvider _weightProvider;
    private readonly ILogger<RecommendationEvaluator> _logger;

    public RecommendationEvaluator(
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ExerciseKnowledgePoint, Guid> mapRepository,
        IRepository<AnswerRecord, Guid> answerRepository,
        IRepository<StudentExerciseRecord> onlineRepository,
        IConfiguration configuration,
        RecommendationWeightProvider weightProvider,
        ILogger<RecommendationEvaluator> logger)
    {
        _exerciseRepository = exerciseRepository;
        _mapRepository = mapRepository;
        _answerRepository = answerRepository;
        _onlineRepository = onlineRepository;
        _configuration = configuration;
        _weightProvider = weightProvider;
        _logger = logger;
    }

    public async Task<RecommendationEvaluationDto> EvaluateAsync(
        Guid? courseId,
        int topK,
        CancellationToken cancellationToken = default)
    {
        topK = Math.Clamp(topK, 1, 20);
        var minAnswers = _configuration.GetValue("LearningAnalytics:EvalMinAnswers", 8);
        var testSize = _configuration.GetValue("LearningAnalytics:EvalTestSize", 3);
        var maxStudents = _configuration.GetValue("LearningAnalytics:EvalMaxStudents", 200);
        var weakRatio = _configuration.GetValue("LearningAnalytics:EvalWeakRatio", 0.6);

        var result = new RecommendationEvaluationDto
        {
            CourseId = courseId,
            TopK = topK,
        };

        // 当前生效权重（设置项优先）
        var currentWeights = await _weightProvider.GetAsync();

        var courseIds = courseId.HasValue
            ? new List<Guid> { courseId.Value }
            : (await _answerRepository.GetListAsync(cancellationToken: cancellationToken))
                .Select(a => a.CourseId).Distinct().ToList();

        // 逐课程处理，避免不同课程的知识点混淆
        var accumulators = CandidateWeightSets.ToDictionary(
            w => w.Name,
            _ => new Accumulator());

        var studentsProcessed = 0;
        var studentsEvaluated = 0;
        var answersUsed = 0;

        foreach (var currentCourseId in courseIds)
        {
            var exercises = await _exerciseRepository.GetListAsync(e => e.CourseId == currentCourseId, cancellationToken: cancellationToken);
            if (exercises.Count < topK + 1)
            {
                continue;
            }

            var exerciseById = exercises.ToDictionary(e => e.Id);
            var maps = await _mapRepository.GetListAsync(
                m => exerciseById.Keys.Contains(m.ExerciseId), cancellationToken: cancellationToken);
            var kpWeightsByExercise = maps
                .GroupBy(m => m.ExerciseId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.KnowledgePointId, x => x.Weight));

            // 统一作答：AnswerRecord（纸质/课堂）+ StudentExerciseRecord（在线），按时间排序
            var answers = (await _answerRepository.GetListAsync(
                    a => a.CourseId == currentCourseId, cancellationToken: cancellationToken))
                .Select(a => new Observation(a.StudentId, a.ExerciseId, a.Ratio, a.AnsweredAt))
                .ToList();

            var online = await _onlineRepository.GetListAsync(
                r => r.CourseId == currentCourseId && r.IsCorrect != null, cancellationToken: cancellationToken);
            answers.AddRange(online.Select(r => new Observation(
                r.StudentId,
                r.ExerciseId,
                r.IsCorrect == true ? 1 : 0,
                r.CompletedAt ?? r.CreationTime)));

            if (!answers.Any())
            {
                continue;
            }

            answersUsed += answers.Count;

            var byStudent = answers
                .GroupBy(a => a.StudentId)
                .Where(g => g.Count() >= minAnswers)
                .Take(maxStudents)
                .ToList();

            if (!byStudent.Any())
            {
                continue;
            }

            // 一次性构建所有学生的历史观测（用于掌握度与相似度）
            var histories = byStudent.ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.At).ToList());

            var masteryByStudent = histories.ToDictionary(
                kvp => kvp.Key,
                kvp => BuildMastery(kvp.Value, kpWeightsByExercise, exerciseById));

            foreach (var (studentId, all) in histories)
            {
                var ordered = all.OrderBy(o => o.At).ToList();
                var test = ordered.TakeLast(testSize).ToList();
                var history = ordered.Take(ordered.Count - test.Count).ToList();

                studentsProcessed++;

                var weakTest = test.Where(t => t.Ratio < weakRatio).Select(t => t.ExerciseId).ToHashSet();
                if (!weakTest.Any())
                {
                    // 测试集全对：无法评估「命中错题」，只计入处理数，不计入评估分母
                    continue;
                }

                // 掌握度只用历史，避免泄漏
                var mastery = BuildMastery(history, kpWeightsByExercise, exerciseById);

                // 相似学生（用历史掌握度向量的余弦）
                var neighbors = FindNeighbors(studentId, mastery, masteryByStudent, take: 20);

                // 邻居在候选题上的错误率（同样只用各自的历史）
                var historyByStudent = histories.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Take(Math.Max(0, kvp.Value.Count - testSize))
                        .GroupBy(x => x.ExerciseId)
                        .ToDictionary(x => x.Key, x => x.Average(y => y.Ratio)));

                // 候选池 = 全部习题 - 历史中已经做对的题（保留测试题，否则无法命中错题；
                // 掌握度仍然只用历史计算，保证不泄漏测试集信息）
                var correctInHistory = history
                    .Where(h => h.Ratio >= 0.9)
                    .Select(h => h.ExerciseId)
                    .ToHashSet();
                var candidates = exercises.Where(e => !correctInHistory.Contains(e.Id)).ToList();
                if (!candidates.Any())
                {
                    continue;
                }

                studentsEvaluated++;

                foreach (var weightSet in CandidateWeightSets)
                {
                    var effectiveWeights = weightSet.Name == "当前配置" ? currentWeights : weightSet.Weights;
                    var accumulator = accumulators[weightSet.Name];
                    accumulator.WeakTotal += weakTest.Count;

                    var top = candidates
                        .Select(e =>
                        {
                            var kpWeights = kpWeightsByExercise.GetValueOrDefault(e.Id) ?? new Dictionary<Guid, double>();
                            var effectiveMastery = RecommendationScorer.EffectiveMastery(mastery, kpWeights);
                            var content = RecommendationScorer.ContentScore(mastery, kpWeights);
                            var collab = CollaborativeError(e.Id, neighbors, historyByStudent);
                            var fit = RecommendationScorer.FitScore(effectiveMastery, e.Difficulty);
                            return new
                            {
                                e.Id,
                                Score = RecommendationScorer.Score(effectiveWeights, content, collab, fit),
                            };
                        })
                        .OrderByDescending(x => x.Score)
                        .Take(topK)
                        .ToList();

                    var hits = top.Count(t => weakTest.Contains(t.Id));
                    accumulator.Hits += hits;
                    accumulator.PrecisionNumerator += hits;
                    accumulator.PrecisionDenominator += top.Count;
                }
            }
        }

        foreach (var (name, weights) in CandidateWeightSets)
        {
            var acc = accumulators[name];
            var effective = name == "当前配置" ? currentWeights : weights;

            result.Results.Add(new RecommendationWeightSetDto
            {
                Name = name,
                Content = effective.Content,
                Collab = effective.Collab,
                Fit = effective.Fit,
                Label = effective.Label,
                WeakHitRate = Math.Round(acc.WeakHitRate, 4),
                Precision = Math.Round(acc.Precision, 4),
                Hits = acc.Hits,
                WeakTotal = acc.WeakTotal,
            });
        }

        result.StudentCount = studentsEvaluated;
        result.EvaluatedStudentCount = studentsProcessed;
        result.AnswerCount = answersUsed;
        result.Current = result.Results.FirstOrDefault(r => r.Name == "当前配置");
        result.Best = result.Results
            .Where(r => r.WeakTotal > 0)
            .OrderByDescending(r => r.WeakHitRate)
            .ThenByDescending(r => r.Precision)
            .FirstOrDefault();

        result.Message = studentsEvaluated == 0
            ? $"样本不足：需至少 {minAnswers} 条作答/学生，且最近 {testSize} 条里要有错题；" +
              $"当前处理 {studentsProcessed} 名学生但无可评估错题样本"
            : $"共处理 {studentsProcessed} 名学生，其中 {studentsEvaluated} 名学生的测试集含错题可用于评估；" +
              $"合计 {answersUsed} 条作答（留出最近 {testSize} 条作测试集，top-{topK}）";

        var best = result.Best;
        if (best != null && result.Current != null)
        {
            var delta = best.WeakHitRate - result.Current.WeakHitRate;
            if (best.Name != "当前配置" && delta > 0.01)
            {
                result.Message += $"；建议权重「{best.Label}」比当前配置命中率高 {delta:P1}";
            }
            else
            {
                result.Message += "；当前配置已接近最优";
            }
        }

        _logger.LogInformation("推荐离线评估完成：{Message}", result.Message);
        return result;
    }

    private record Observation(Guid StudentId, Guid ExerciseId, double Ratio, DateTime At);

    /// <summary>协同信号：相似学生在该题上的加权错误率（只用各自历史作答）。</summary>
    private static double CollaborativeError(
        Guid exerciseId,
        IReadOnlyList<(Guid StudentId, double Similarity)> neighbors,
        Dictionary<Guid, Dictionary<Guid, double>> historyByStudent)
    {
        double weighted = 0, total = 0;
        foreach (var (neighborId, similarity) in neighbors)
        {
            if (!historyByStudent.TryGetValue(neighborId, out var historyMap) ||
                !historyMap.TryGetValue(exerciseId, out var ratio))
            {
                continue;
            }

            weighted += similarity * (1 - ratio);
            total += similarity;
        }

        return total > 0 ? Math.Clamp(weighted / total, 0, 1) : 0;
    }

    private sealed class Accumulator
    {
        public int Hits { get; set; }
        public int WeakTotal { get; set; }
        public int PrecisionNumerator { get; set; }
        public int PrecisionDenominator { get; set; }

        public double WeakHitRate => WeakTotal > 0 ? (double)Hits / WeakTotal : 0;

        public double Precision => PrecisionDenominator > 0 ? (double)PrecisionNumerator / PrecisionDenominator : 0;
    }

    /// <summary>用给定观测集合按知识点累计 Beta 平滑掌握度。</summary>
    private static Dictionary<Guid, double> BuildMastery(
        IReadOnlyList<Observation> observations,
        Dictionary<Guid, Dictionary<Guid, double>> kpWeightsByExercise,
        Dictionary<Guid, Exercise> exerciseById)
    {
        var buckets = new Dictionary<Guid, List<MasteryCalculator.Observation>>();
        var now = DateTime.UtcNow;

        foreach (var observation in observations)
        {
            if (!kpWeightsByExercise.TryGetValue(observation.ExerciseId, out var kpWeights))
            {
                continue;
            }

            var fullScore = exerciseById.TryGetValue(observation.ExerciseId, out var exercise) && exercise.Score > 0
                ? exercise.Score
                : 1.0;

            foreach (var (kpId, weight) in kpWeights)
            {
                if (!buckets.TryGetValue(kpId, out var bucket))
                {
                    bucket = new List<MasteryCalculator.Observation>();
                    buckets[kpId] = bucket;
                }

                bucket.Add(new MasteryCalculator.Observation(
                    Math.Clamp(observation.Ratio, 0, 1),
                    MasteryCalculator.WeightFromFullScore(weight, fullScore),
                    observation.At));
            }
        }

        var mastery = new Dictionary<Guid, double>();
        foreach (var (kpId, bucket) in buckets)
        {
            var (value, _) = MasteryCalculator.Compute(bucket, now);
            mastery[kpId] = Math.Round(value, 4);
        }

        return mastery;
    }

    private static List<(Guid StudentId, double Similarity)> FindNeighbors(
        Guid self,
        Dictionary<Guid, double> mine,
        Dictionary<Guid, Dictionary<Guid, double>> all,
        int take)
    {
        if (mine.Count == 0) return new List<(Guid, double)>();
        var mineNorm = Math.Sqrt(mine.Values.Sum(v => v * v));
        if (mineNorm <= 0) return new List<(Guid, double)>();

        var scores = new List<(Guid, double)>();
        foreach (var (otherId, vector) in all)
        {
            if (otherId == self || vector.Count == 0) continue;

            double dot = 0, norm = 0;
            foreach (var (kpId, value) in vector)
            {
                norm += value * value;
                if (mine.TryGetValue(kpId, out var own)) dot += own * value;
            }

            if (dot <= 0 || norm <= 0) continue;
            scores.Add((otherId, dot / (mineNorm * Math.Sqrt(norm))));
        }

        return scores.OrderByDescending(s => s.Item2).Take(take).ToList();
    }
}
