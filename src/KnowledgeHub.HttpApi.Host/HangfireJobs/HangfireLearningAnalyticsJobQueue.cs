using System;
using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.LearningAnalytics;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 实现的智学分析队列（PostgreSQL 持久化）。
/// OCR 与知识点抽取较长，入 "ai" 队列；模型训练 / 挖掘入 "default" 队列。
/// </summary>
public class HangfireLearningAnalyticsJobQueue : ILearningAnalyticsJobQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireLearningAnalyticsJobQueue(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void EnqueuePaperOcr(Guid batchId, Guid? tenantId)
    {
        _backgroundJobClient.Enqueue<PaperOcrProcessingJob>(
            "ai",
            job => job.ExecuteAsync(batchId, tenantId));
    }

    public void EnqueueKnowledgePointExtraction(Guid courseId, bool overwrite, Guid? tenantId)
    {
        _backgroundJobClient.Enqueue<KpExtractionJob>(
            "ai",
            job => job.ExecuteAsync(courseId, overwrite, tenantId));
    }

    public void EnqueueModelTraining(Guid? tenantId)
    {
        _backgroundJobClient.Enqueue<MasteryModelTrainJob>(
            "default",
            job => job.ExecuteAsync(tenantId));
    }

    public void EnqueuePrerequisiteMining(Guid? courseId, Guid? tenantId)
    {
        _backgroundJobClient.Enqueue<PrerequisiteMiningJob>(
            "default",
            job => job.ExecuteAsync(courseId, tenantId));
    }
}
