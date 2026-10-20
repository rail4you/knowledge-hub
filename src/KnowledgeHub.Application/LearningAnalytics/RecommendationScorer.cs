using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 推荐三路权重：内容（薄弱向量 × 习题知识点）、协同（相似学生错误率）、难度适配。
/// 权重可通过 <c>LearningAnalytics:RecWeights:{Content,Collab,Fit}</c> 配置，默认 0.5 / 0.3 / 0.2。
/// </summary>
public readonly record struct RecommendationWeights(double Content, double Collab, double Fit)
{
    public static RecommendationWeights Default => new(0.5, 0.3, 0.2);

    public static RecommendationWeights FromConfiguration(IConfiguration configuration) => new(
        configuration.GetValue("LearningAnalytics:RecWeights:Content", 0.5),
        configuration.GetValue("LearningAnalytics:RecWeights:Collab", 0.3),
        configuration.GetValue("LearningAnalytics:RecWeights:Fit", 0.2));

    public string Label => $"内容 {Content:0.##} / 协同 {Collab:0.##} / 难度 {Fit:0.##}";
}

/// <summary>推荐打分（纯函数，便于离线评估复用同一套逻辑）。</summary>
public static class RecommendationScorer
{
    /// <summary>难度适配得分：预测答对概率越接近 0.65 越好（跳一跳够得着）。</summary>
    public static double FitScore(double mastery, int difficulty)
    {
        var d = Math.Clamp((difficulty - 1) / 4.0, 0, 1);
        var p = 1 / (1 + Math.Exp(-6 * (mastery - d)));
        return 1 - Math.Abs(p - 0.65) / 0.65;
    }

    /// <summary>
    /// 内容相似度：习题各知识点「薄弱程度」的加权和（1 - 掌握度）。
    /// 未测过的知识点按 <paramref name="fallbackMastery"/> 计。
    /// </summary>
    public static double ContentScore(
        IReadOnlyDictionary<Guid, double> mastery,
        IReadOnlyDictionary<Guid, double> kpWeights,
        double fallbackMastery = 0.5)
    {
        if (kpWeights.Count == 0)
        {
            return 0;
        }

        double sum = 0, weightSum = 0;
        foreach (var (kpId, weight) in kpWeights)
        {
            var level = mastery.GetValueOrDefault(kpId, fallbackMastery);
            sum += weight * Math.Clamp(1 - level, 0, 1);
            weightSum += Math.Abs(weight);
        }

        return weightSum > 0 ? Math.Clamp(sum / weightSum, 0, 1) : 0;
    }

    /// <summary>习题的平均掌握度（用于难度适配与预测答对概率）。</summary>
    public static double EffectiveMastery(
        IReadOnlyDictionary<Guid, double> mastery,
        IReadOnlyDictionary<Guid, double> kpWeights,
        double fallbackMastery = 0.5)
    {
        if (kpWeights.Count == 0)
        {
            return fallbackMastery;
        }

        double sum = 0, weightSum = 0;
        foreach (var (kpId, weight) in kpWeights)
        {
            sum += weight * mastery.GetValueOrDefault(kpId, fallbackMastery);
            weightSum += Math.Abs(weight);
        }

        return weightSum > 0 ? Math.Clamp(sum / weightSum, 0, 1) : fallbackMastery;
    }

    public static double Score(in RecommendationWeights weights, double content, double collab, double fit) =>
        weights.Content * content + weights.Collab * collab + weights.Fit * fit;
}
