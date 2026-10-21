using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Exams;
using KnowledgeHub.LearningAnalytics.Enums;
using KnowledgeHub.LearningAnalytics.Ocr;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 试卷 OCR 后台任务：逐张图片识别 → 入库为待校对试卷 → 刷新批次统计。
/// 通过 <see cref="ILearningAnalyticsJobQueue"/> 入 Hangfire 队列；未注册队列时由应用服务同步调用。
/// </summary>
public class PaperOcrProcessingJob : ITransientDependency
{
    private readonly IRepository<PaperBatch, Guid> _batchRepository;
    private readonly IRepository<PaperUploadImage, Guid> _imageRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly PaperOcrRecognizerResolver _recognizerResolver;
    private readonly PaperImageStore _imageStore;
    private readonly PaperIngestionService _ingestion;
    private readonly PaperBatchStatsService _stats;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<PaperOcrProcessingJob> _logger;

    public PaperOcrProcessingJob(
        IRepository<PaperBatch, Guid> batchRepository,
        IRepository<PaperUploadImage, Guid> imageRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        PaperOcrRecognizerResolver recognizerResolver,
        PaperImageStore imageStore,
        PaperIngestionService ingestion,
        PaperBatchStatsService stats,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<PaperOcrProcessingJob> logger)
    {
        _batchRepository = batchRepository;
        _imageRepository = imageRepository;
        _exerciseRepository = exerciseRepository;
        _recognizerResolver = recognizerResolver;
        _imageStore = imageStore;
        _ingestion = ingestion;
        _stats = stats;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid batchId, Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            var batch = await _batchRepository.FindAsync(batchId);
            if (batch == null)
            {
                _logger.LogWarning("PaperOCR job: batch {BatchId} not found (tenant {TenantId})", batchId, tenantId);
                return;
            }

            var images = (await _imageRepository.GetListAsync(i => i.BatchId == batchId))
                .Where(i => i.Status == PaperUploadStatus.Pending || i.Status == PaperUploadStatus.Processing)
                .OrderBy(i => i.CreationTime)
                .ToList();

            if (!images.Any())
            {
                await _stats.RefreshAsync(batchId);
                await uow.CompleteAsync();
                return;
            }

            // 真实 OCR（Qwen-VL-OCR）：未配置 Key 时直接标记失败，不生成假成绩
            IPaperOcrRecognizer recognizer;
            try
            {
                recognizer = await _recognizerResolver.ResolveAsync(true);
            }
            catch (Exception ex)
            {
                foreach (var image in images)
                {
                    image.Status = PaperUploadStatus.Failed;
                    image.ErrorMessage = ex is Volo.Abp.UserFriendlyException
                        ? ex.Message
                        : "OCR 识别器不可用：" + ex.Message;
                    image.ProcessedAt = DateTime.UtcNow;
                    await _imageRepository.UpdateAsync(image, autoSave: true);
                }

                await _stats.RefreshAsync(batchId);
                await uow.CompleteAsync();
                return;
            }

            if (!(await _exerciseRepository.GetListAsync(e => e.CourseId == batch.CourseId)).Any())
            {
                foreach (var image in images)
                {
                    image.Status = PaperUploadStatus.Failed;
                    image.ErrorMessage = "该课程还没有习题，无法把试卷题目映射到知识点，请先建题";
                    image.ProcessedAt = DateTime.UtcNow;
                    await _imageRepository.UpdateAsync(image, autoSave: true);
                }

                await _stats.RefreshAsync(batchId);
                await uow.CompleteAsync();
                return;
            }

            // 按「合并分组」处理：同一份多页试卷的多张图片合并为一个 PaperScript。
            // 分组键带上学生 Id，避免不同学生上传同名文件（如都叫 paper.pdf）时被并成一份卷。
            var groups = images.GroupBy(i =>
                string.IsNullOrWhiteSpace(i.MergeGroup)
                    ? $"single:{i.Id:N}"
                    : $"merge:{i.StudentId?.ToString("N") ?? "none"}:{i.MergeGroup}");

            foreach (var group in groups)
            {
                var pages = group
                    .OrderBy(i => i.PageIndex)
                    .ThenBy(i => i.CreationTime)
                    .ToList();

                var results = new List<PaperOcrResult>();

                foreach (var image in pages)
                {
                    image.Status = PaperUploadStatus.Processing;
                    image.OcrProvider = recognizer.Provider;
                    await _imageRepository.UpdateAsync(image, autoSave: true);

                    try
                    {
                        // 两种来源：OSS 图片走 URL（不读磁盘），本地上传走字节
                        byte[] bytes = Array.Empty<byte>();
                        string? imageUrl = null;
                        string? pdfUrl = null;
                        if (image.StoredPath.StartsWith("url:", StringComparison.OrdinalIgnoreCase))
                        {
                            var url = image.StoredPath.Substring("url:".Length);
                            if (string.Equals(image.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                pdfUrl = url;
                            }
                            else
                            {
                                imageUrl = url;
                            }
                        }
                        else
                        {
                            bytes = await _imageStore.ReadAsync(image.StoredPath);
                        }

                        var result = await recognizer.RecognizeAsync(new PaperOcrRequest
                        {
                            Content = bytes,
                            ImageUrl = imageUrl,
                            PdfUrl = pdfUrl,
                            ContentType = image.ContentType,
                            FileName = image.FileName,
                            CourseName = batch.CourseName,
                        });

                        image.OcrModel = result.Model;
                        image.OcrRawJson = result.RawJson;
                        image.ProcessedAt = DateTime.UtcNow;
                        image.ErrorMessage = null;
                        await _imageRepository.UpdateAsync(image, autoSave: true);

                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        image.Status = PaperUploadStatus.Failed;
                        image.ErrorMessage = ex is Volo.Abp.UserFriendlyException
                            ? ex.Message
                            : "OCR 识别失败：" + ex.Message;
                        image.ProcessedAt = DateTime.UtcNow;
                        await _imageRepository.UpdateAsync(image, autoSave: true);

                        _logger.LogError(ex, "PaperOCR job: image {FileName} failed", image.FileName);
                    }
                }

                if (!results.Any())
                {
                    continue;
                }

                try
                {
                    var merged = PaperOcrResultMerger.Merge(results);

                    // 归属学生：优先用上传时教师指定的学生（图片上的 StudentId），
                    // 没有则回退到 OCR 识别出的学号/姓名。
                    var owner = pages.FirstOrDefault(p => p.StudentId.HasValue);
                    var script = await _ingestion.IngestAsync(
                        batch.Id,
                        batch.CourseId,
                        pages[0].FileName,
                        merged,
                        owner?.StudentId,
                        owner?.StudentName,
                        owner?.StudentNumber);

                    foreach (var image in pages.Where(i => i.Status != PaperUploadStatus.Failed))
                    {
                        image.Status = PaperUploadStatus.Done;
                        image.ScriptId = script.Id;
                        await _imageRepository.UpdateAsync(image, autoSave: true);
                    }

                    _logger.LogInformation(
                        "PaperOCR job: {File} -> script {ScriptId}（{Pages} 页, {Count} 题, 置信度 {Confidence}）",
                        pages[0].FileName, script.Id, pages.Count, merged.Questions.Count, merged.Confidence);
                }
                catch (Exception ex)
                {
                    foreach (var image in pages.Where(i => i.Status != PaperUploadStatus.Failed))
                    {
                        image.Status = PaperUploadStatus.Failed;
                        image.ErrorMessage = "识别结果入库失败：" + ex.Message;
                        await _imageRepository.UpdateAsync(image, autoSave: true);
                    }

                    _logger.LogError(ex, "PaperOCR job: 合并入库失败 {File}", pages[0].FileName);
                }
            }

            await _stats.RefreshAsync(batchId);
            await uow.CompleteAsync();
        }
    }
}
