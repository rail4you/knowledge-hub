using System;
using KnowledgeHub.LearningAnalytics.Enums;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 智学分析后台队列（Host 用 Hangfire 实现；未注册时应用服务会退化为同步执行）。
/// 统一显式携带 TenantId：后台线程没有请求上下文，必须靠参数还原租户。
/// </summary>
public interface ILearningAnalyticsJobQueue
{
    /// <summary>入队：试卷图片 OCR + 入库</summary>
    void EnqueuePaperOcr(Guid batchId, Guid? tenantId);

    /// <summary>
    /// 入队：知识点生成。
    /// mode=Full 时章节 → 知识点、习题 → 知识点关联全量刷新，并重建语义索引；
    /// mode=ExerciseIncremental / ExerciseOverwrite 时只处理习题关联。
    /// </summary>
    void EnqueueKnowledgePointGeneration(Guid courseId, KpGenerationMode mode, Guid? tenantId);

    /// <summary>入队：掌握度模型重训</summary>
    void EnqueueModelTraining(Guid? tenantId);

    /// <summary>入队：先修关系挖掘</summary>
    void EnqueuePrerequisiteMining(Guid? courseId, Guid? tenantId);
}
