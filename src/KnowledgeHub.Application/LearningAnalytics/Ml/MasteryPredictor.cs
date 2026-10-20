using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics.Ml;

/// <summary>
/// 掌握度预测器：补全学生未作答知识点的掌握度，并为协同推荐提供「相似学生」。
/// </summary>
public interface IMasteryPredictor
{
    /// <summary>估计器名称（ml-mf / baseline / empty），用于健康检查与提示</summary>
    string Estimator { get; }

    bool IsReady { get; }

    int SampleCount { get; }

    int StudentCount { get; }

    int KnowledgePointCount { get; }

    /// <summary>预测掌握度（0~1）；无信息时返回兜底值</summary>
    double Predict(Guid studentId, Guid knowledgePointId);

    /// <summary>按掌握度向量取最相似的学生</summary>
    IReadOnlyList<(Guid StudentId, double Similarity)> Neighbors(Guid studentId, int take);
}

/// <summary>空预测器：未训练时使用，所有预测返回 0.5（不改变推荐排序）。</summary>
public class EmptyMasteryPredictor : IMasteryPredictor
{
    public string Estimator => "empty";

    public bool IsReady => false;

    public int SampleCount => 0;

    public int StudentCount => 0;

    public int KnowledgePointCount => 0;

    public double Predict(Guid studentId, Guid knowledgePointId) => 0.5;

    public IReadOnlyList<(Guid StudentId, double Similarity)> Neighbors(Guid studentId, int take) =>
        Array.Empty<(Guid, double)>();
}

/// <summary>
/// 兜底预测器（数据不足 / ML.NET 原生库不可用）：全局均值 + 学生偏移 + 知识点偏移。
/// 比统一 0.5 更有信息量，且完全无原生依赖。
/// </summary>
public class BaselineMasteryPredictor : IMasteryPredictor
{
    private readonly double _globalMean;
    private readonly Dictionary<Guid, double> _studentMean;
    private readonly Dictionary<Guid, double> _kpMean;
    private readonly Dictionary<Guid, Dictionary<Guid, double>> _studentVectors;

    public BaselineMasteryPredictor(
        double globalMean,
        Dictionary<Guid, double> studentMean,
        Dictionary<Guid, double> kpMean,
        Dictionary<Guid, Dictionary<Guid, double>> studentVectors)
    {
        _globalMean = globalMean;
        _studentMean = studentMean;
        _kpMean = kpMean;
        _studentVectors = studentVectors;
    }

    public string Estimator => "baseline";

    public bool IsReady => true;

    public int SampleCount => _studentVectors.Sum(v => v.Value.Count);

    public int StudentCount => _studentVectors.Count;

    public int KnowledgePointCount => _kpMean.Count;

    public double Predict(Guid studentId, Guid knowledgePointId)
    {
        var studentOffset = _studentMean.TryGetValue(studentId, out var sm) ? sm - _globalMean : 0;
        var kpOffset = _kpMean.TryGetValue(knowledgePointId, out var km) ? km - _globalMean : 0;
        return Math.Clamp(_globalMean + studentOffset + kpOffset, 0.05, 0.95);
    }

    public IReadOnlyList<(Guid StudentId, double Similarity)> Neighbors(Guid studentId, int take)
    {
        if (!_studentVectors.TryGetValue(studentId, out var mine)) return Array.Empty<(Guid, double)>();
        return MasterySimilarity.TopNeighbors(studentId, mine, _studentVectors, take);
    }
}

/// <summary>共用的向量余弦相似度工具（供 baseline 与 ML 预测器复用）。</summary>
public static class MasterySimilarity
{
    public static IReadOnlyList<(Guid StudentId, double Similarity)> TopNeighbors(
        Guid self,
        Dictionary<Guid, double> mine,
        Dictionary<Guid, Dictionary<Guid, double>> all,
        int take)
    {
        if (take <= 0 || mine.Count == 0) return Array.Empty<(Guid, double)>();

        var mineNorm = Math.Sqrt(mine.Values.Sum(v => v * v));
        if (mineNorm <= 0) return Array.Empty<(Guid, double)>();

        var scores = new List<(Guid, double)>();
        foreach (var (otherId, vector) in all)
        {
            if (otherId == self || vector.Count == 0) continue;

            double dot = 0, norm = 0;
            foreach (var (kp, value) in vector)
            {
                norm += value * value;
                if (mine.TryGetValue(kp, out var m)) dot += m * value;
            }

            if (dot <= 0 || norm <= 0) continue;
            scores.Add((otherId, dot / (mineNorm * Math.Sqrt(norm))));
        }

        return scores.OrderByDescending(s => s.Item2).Take(take).ToList();
    }
}

/// <summary>
/// 预测器持有者（单例，按租户隔离）。夜间任务训练后热替换，请求线程无锁读取。
/// </summary>
public class MasteryPredictorHolder : ISingletonDependency
{
    private readonly ConcurrentDictionary<Guid, IMasteryPredictor> _byTenant = new();

    public IMasteryPredictor Get(Guid? tenantId)
    {
        var key = tenantId ?? HostKey;
        return _byTenant.TryGetValue(key, out var predictor) ? predictor : EmptyMasteryPredictorInstance;
    }

    public void Set(Guid? tenantId, IMasteryPredictor predictor)
    {
        _byTenant[tenantId ?? HostKey] = predictor;
    }

    private static readonly Guid HostKey = Guid.Empty;

    private static readonly IMasteryPredictor EmptyMasteryPredictorInstance = new EmptyMasteryPredictor();
}
