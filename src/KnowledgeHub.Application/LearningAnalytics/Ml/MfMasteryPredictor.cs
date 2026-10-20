using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace KnowledgeHub.LearningAnalytics.Ml;

/// <summary>
/// ML.NET 矩阵分解预测器（LIBMF）。学生 × 知识点矩阵，值为掌握度。
/// 用途：补全未作答知识点的预测掌握度 + 提供相似学生用于协同推荐。
/// </summary>
public class MfMasteryPredictor : IMasteryPredictor, IDisposable
{
    private const int MaxKeySpace = 1_000_000;

    private readonly MLContext _mlContext;
    private readonly ITransformer _model;
    private readonly PredictionEngine<MasteryPredictionInput, MasteryPrediction> _engine;
    private readonly Dictionary<Guid, uint> _studentIndex;
    private readonly Dictionary<Guid, uint> _kpIndex;
    private readonly Dictionary<Guid, Dictionary<Guid, double>> _studentVectors;
    private readonly double _fallback;
    private readonly object _sync = new();

    public MfMasteryPredictor(
        MLContext mlContext,
        ITransformer model,
        Dictionary<Guid, uint> studentIndex,
        Dictionary<Guid, uint> kpIndex,
        Dictionary<Guid, Dictionary<Guid, double>> studentVectors,
        double fallback,
        int sampleCount)
    {
        _mlContext = mlContext;
        _model = model;
        _engine = mlContext.Model.CreatePredictionEngine<MasteryPredictionInput, MasteryPrediction>(model);
        _studentIndex = studentIndex;
        _kpIndex = kpIndex;
        _studentVectors = studentVectors;
        _fallback = fallback;
        SampleCount = sampleCount;
    }

    public string Estimator => "ml-mf";

    public bool IsReady => true;

    public int SampleCount { get; }

    public int StudentCount => _studentIndex.Count;

    public int KnowledgePointCount => _kpIndex.Count;

    public double Predict(Guid studentId, Guid knowledgePointId)
    {
        if (!_studentIndex.TryGetValue(studentId, out var student) ||
            !_kpIndex.TryGetValue(knowledgePointId, out var kp))
        {
            return _fallback;
        }

        try
        {
            lock (_sync)
            {
                var prediction = _engine.Predict(new MasteryPredictionInput { Student = student, Kp = kp });
                return Math.Clamp(prediction.Score, 0.02, 0.98);
            }
        }
        catch
        {
            return _fallback;
        }
    }

    public IReadOnlyList<(Guid StudentId, double Similarity)> Neighbors(Guid studentId, int take)
    {
        if (!_studentVectors.TryGetValue(studentId, out var mine))
        {
            return Array.Empty<(Guid, double)>();
        }

        return MasterySimilarity.TopNeighbors(studentId, mine, _studentVectors, take);
    }

    public void Dispose() => _engine.Dispose();

    // ── ML.NET 数据模型 ──

    public sealed class MasteryCell
    {
        [KeyType(MaxKeySpace)] public uint Student { get; set; }

        [KeyType(MaxKeySpace)] public uint Kp { get; set; }

        public float Label { get; set; }
    }

    public sealed class MasteryPredictionInput
    {
        public uint Student { get; set; }

        public uint Kp { get; set; }
    }

    public sealed class MasteryPrediction
    {
        [ColumnName("Score")] public float Score { get; set; }
    }
}
