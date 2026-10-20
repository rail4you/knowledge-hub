using System;
using System.Collections.Generic;
using System.Linq;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>判定等级（与前端 MasteryLevel 一致：0 薄弱 / 1 待巩固 / 2 达标）</summary>
public enum MasteryGrade
{
    Weak = 0,
    Fair = 1,
    Ok = 2
}

/// <summary>判定线</summary>
public readonly record struct Thresholds(double Weak, double Ok)
{
    public static readonly Thresholds Default = new(0.60, 0.75);

    public MasteryGrade Judge(double mastery) =>
        mastery < Weak ? MasteryGrade.Weak : mastery < Ok ? MasteryGrade.Fair : MasteryGrade.Ok;
}

/// <summary>
/// 掌握度计算：Beta 先验平滑 + 时间半衰期衰减。
/// 避免「只做一题就 0% 或 100%」，并让旧成绩权重逐步降低。
/// </summary>
public static class MasteryCalculator
{
    /// <summary>Beta 先验（等价于 2 次「半对」的虚拟观测）</summary>
    public const double PriorA = 2;

    public const double PriorB = 2;

    /// <summary>半衰期（天）</summary>
    public const double HalfLifeDays = 120;

    public readonly record struct Observation(double Ratio, double Weight, DateTime AnsweredAt);

    public static (double Mastery, double Evidence) Compute(
        IEnumerable<Observation> observations,
        DateTime now)
    {
        double a = PriorA, b = PriorB;
        foreach (var o in observations)
        {
            var days = Math.Max(0, (now - o.AnsweredAt).TotalDays);
            var w = o.Weight * Math.Pow(0.5, days / HalfLifeDays);
            var ratio = Math.Clamp(o.Ratio, 0, 1);
            a += w * ratio;
            b += w * (1 - ratio);
        }

        var mastery = a / (a + b);
        return (mastery, Math.Max(0, a + b - PriorA - PriorB));
    }

    /// <summary>得分率 → 单次观测权重（满分越大的题贡献越大，做截断避免一题压倒全部）</summary>
    public static double WeightFromFullScore(double kpWeight, double fullScore)
    {
        var scale = fullScore > 0 ? Math.Clamp(fullScore / 10.0, 0.2, 3.0) : 1.0;
        return Math.Max(0.01, kpWeight) * scale;
    }

    /// <summary>总体标准差</summary>
    public static double StdDev(IReadOnlyCollection<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return Math.Sqrt(Math.Max(0, variance));
    }

    /// <summary>皮尔逊相关系数（先修关系挖掘用）</summary>
    public static (double R, int N) Correlation(
        IReadOnlyDictionary<Guid, double> x,
        IReadOnlyDictionary<Guid, double> y)
    {
        var ids = x.Keys.Intersect(y.Keys).ToList();
        if (ids.Count < 2) return (0, ids.Count);

        double mx = ids.Average(i => x[i]), my = ids.Average(i => y[i]);
        double sxy = 0, sxx = 0, syy = 0;
        foreach (var i in ids)
        {
            var dx = x[i] - mx;
            var dy = y[i] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }

        if (sxx <= 0 || syy <= 0) return (0, ids.Count);
        return (sxy / Math.Sqrt(sxx * syy), ids.Count);
    }
}
