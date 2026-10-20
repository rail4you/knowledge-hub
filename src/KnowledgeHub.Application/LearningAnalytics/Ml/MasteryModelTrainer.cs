using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.LearningAnalytics.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics.Ml;

/// <summary>
/// 掌握度矩阵分解训练器（ML.NET）。
///
/// 训练完成后把预测器写入 <see cref="MasteryPredictorHolder"/>（按租户隔离），
/// 请求线程直接读取，实现「夜间重训、白天热用」。
/// 数据不足或原生库不可用时回退 <see cref="BaselineMasteryPredictor"/>，保证功能不失效。
/// </summary>
public class MasteryModelTrainer : ITransientDependency
{
    private readonly IRepository<StudentKpMastery, Guid> _masteryRepository;
    private readonly MasteryPredictorHolder _holder;
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<MasteryModelTrainer> _logger;

    public MasteryModelTrainer(
        IRepository<StudentKpMastery, Guid> masteryRepository,
        MasteryPredictorHolder holder,
        IConfiguration configuration,
        ICurrentTenant currentTenant,
        ILogger<MasteryModelTrainer> logger)
    {
        _masteryRepository = masteryRepository;
        _holder = holder;
        _configuration = configuration;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<ModelTrainResultDto> TrainAsync(CancellationToken cancellationToken = default)
    {
        var enabled = _configuration.GetValue("LearningAnalytics:MatrixFactorizationEnabled", true);
        var minSamples = _configuration.GetValue("LearningAnalytics:MfMinSamples", 30);
        var rank = _configuration.GetValue("LearningAnalytics:MfRank", 16);

        var rows = (await _masteryRepository.GetListAsync(x => x.Evidence >= 0.5, cancellationToken: cancellationToken))
            .Where(x => x.Mastery is > 0 and < 1.0001)
            .ToList();

        var result = new ModelTrainResultDto
        {
            SampleCount = rows.Count,
            StudentCount = rows.Select(r => r.StudentId).Distinct().Count(),
            KnowledgePointCount = rows.Select(r => r.KnowledgePointId).Distinct().Count(),
        };

        // 训练样本太少：矩阵分解没有意义，直接用基线预测器
        if (!enabled || rows.Count < minSamples || result.StudentCount < 2 || result.KnowledgePointCount < 2)
        {
            var baseline = BuildBaseline(rows);
            _holder.Set(_currentTenant.Id, baseline);
            result.Trained = baseline.IsReady;
            result.Estimator = baseline.Estimator;
            result.Message = !enabled
                ? "矩阵分解已关闭（LearningAnalytics:MatrixFactorizationEnabled=false），使用基线预测器"
                : $"样本不足（{rows.Count} < {minSamples}）或维度不足，使用基线预测器";
            _logger.LogInformation("掌握度模型：{Message}", result.Message);
            return result;
        }

        try
        {
            var predictor = TrainMf(rows, rank, minSamples);
            _holder.Set(_currentTenant.Id, predictor);
            result.Trained = true;
            result.Estimator = predictor.Estimator;
            result.Message = $"矩阵分解训练完成（rank={rank}，样本 {rows.Count}）";
            _logger.LogInformation("掌握度模型：{Message}", result.Message);
            return result;
        }
        catch (Exception ex)
        {
            // 原生库（libmf）缺失等环境问题 → 回退基线，不影响业务
            _logger.LogWarning(ex, "ML.NET 矩阵分解训练失败，回退基线预测器");
            var baseline = BuildBaseline(rows);
            _holder.Set(_currentTenant.Id, baseline);
            result.Trained = baseline.IsReady;
            result.Estimator = baseline.Estimator;
            result.Message = "矩阵分解训练失败（可能是原生库不可用），已回退基线预测器：" + ex.Message;
            return result;
        }
    }

    private static BaselineMasteryPredictor BuildBaseline(List<StudentKpMastery> rows)
    {
        var globalMean = rows.Any() ? rows.Average(r => r.Mastery) : 0.5;
        var studentMean = rows.GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Mastery));
        var kpMean = rows.GroupBy(r => r.KnowledgePointId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Mastery));
        var vectors = rows.GroupBy(r => r.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.KnowledgePointId)
                    .ToDictionary(x => x.Key, x => x.Average(y => y.Mastery)));

        return new BaselineMasteryPredictor(globalMean, studentMean, kpMean, vectors);
    }

    private static MfMasteryPredictor TrainMf(List<StudentKpMastery> rows, int rank, int minSamples)
    {
        var studentIndex = rows.Select(r => r.StudentId).Distinct()
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => (uint)x.i);
        var kpIndex = rows.Select(r => r.KnowledgePointId).Distinct()
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => (uint)x.i);

        var mlContext = new MLContext(seed: 7);
        var data = mlContext.Data.LoadFromEnumerable(rows.Select(r => new MfMasteryPredictor.MasteryCell
        {
            Student = studentIndex[r.StudentId],
            Kp = kpIndex[r.KnowledgePointId],
            Label = (float)r.Mastery,
        }));

        var options = new MatrixFactorizationTrainer.Options
        {
            MatrixColumnIndexColumnName = nameof(MfMasteryPredictor.MasteryCell.Student),
            MatrixRowIndexColumnName = nameof(MfMasteryPredictor.MasteryCell.Kp),
            LabelColumnName = nameof(MfMasteryPredictor.MasteryCell.Label),
            LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossRegression,
            ApproximationRank = Math.Clamp(rank, 4, 64),
            Lambda = 0.05,
            NumberOfIterations = 40,
            C = 0.00001,
            Quiet = true,
        };

        var model = mlContext.Recommendation().Trainers.MatrixFactorization(options).Fit(data);

        var vectors = rows.GroupBy(r => r.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.KnowledgePointId)
                    .ToDictionary(x => x.Key, x => x.Average(y => y.Mastery)));

        var fallback = rows.Average(r => r.Mastery);

        return new MfMasteryPredictor(
            mlContext, model, studentIndex, kpIndex, vectors, fallback, rows.Count);
    }
}
