using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 批次统计与状态推进（试卷份数 / 平均分 / 待校对 / OCR 进度），供 OCR 任务与校对接口复用。
/// </summary>
public class PaperBatchStatsService : ITransientDependency
{
    private readonly IRepository<PaperBatch, Guid> _batchRepository;
    private readonly IRepository<PaperScript, Guid> _scriptRepository;
    private readonly IRepository<PaperUploadImage, Guid> _imageRepository;

    public PaperBatchStatsService(
        IRepository<PaperBatch, Guid> batchRepository,
        IRepository<PaperScript, Guid> scriptRepository,
        IRepository<PaperUploadImage, Guid> imageRepository)
    {
        _batchRepository = batchRepository;
        _scriptRepository = scriptRepository;
        _imageRepository = imageRepository;
    }

    public async Task RefreshAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.FindAsync(batchId, cancellationToken: cancellationToken);
        if (batch == null)
        {
            return;
        }

        var scripts = await _scriptRepository.GetListAsync(s => s.BatchId == batchId, cancellationToken: cancellationToken);
        var images = await _imageRepository.GetListAsync(i => i.BatchId == batchId, cancellationToken: cancellationToken);

        batch.ScriptCount = scripts.Count;
        batch.NeedsConfirmCount = scripts.Count(s => !s.Confirmed);
        batch.AverageScore = scripts.Any() ? Math.Round(scripts.Average(s => s.TotalScore), 1) : 0;

        var hasPendingOcr = images.Any(i =>
            i.Status == PaperUploadStatus.Pending || i.Status == PaperUploadStatus.Processing);

        if (hasPendingOcr)
        {
            batch.Status = PaperBatchStatus.OcrRunning;
        }
        else if (scripts.Any() && scripts.All(s => s.Confirmed))
        {
            batch.Status = batch.Status == PaperBatchStatus.Pushed
                ? PaperBatchStatus.Pushed
                : PaperBatchStatus.Confirmed;
        }
        else if (scripts.Any())
        {
            batch.Status = PaperBatchStatus.NeedConfirm;
        }
        else
        {
            batch.Status = PaperBatchStatus.Draft;
        }

        await _batchRepository.UpdateAsync(batch, autoSave: true, cancellationToken: cancellationToken);
    }
}
