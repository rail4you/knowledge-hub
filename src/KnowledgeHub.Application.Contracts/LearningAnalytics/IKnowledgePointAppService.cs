using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.LearningAnalytics.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 知识点管理（归一化闭环）：列表 / 编辑 / 确认灰区 / 合并 / 拆分 / 习题关联 / 合并建议 / 向量重建。
/// 路由前缀（ABP 约定）：<c>/api/app/knowledge-point</c>。
/// </summary>
public interface IKnowledgePointAppService : IApplicationService
{
    Task<PagedResultDto<KnowledgePointDetailDto>> GetListAsync(GetKnowledgePointListInput input);

    Task<KnowledgePointDetailDto> GetAsync(Guid id);

    Task<KnowledgePointDetailDto> UpdateAsync(Guid id, UpdateKnowledgePointInput input);

    /// <summary>把灰区（PendingReview）知识点确认为正式知识点</summary>
    Task<KnowledgePointDetailDto> ConfirmAsync(Guid id);

    Task<KnowledgePointMergeResultDto> MergeAsync(MergeKnowledgePointsInput input);

    Task<KnowledgePointDetailDto> SplitAsync(SplitKnowledgePointInput input);

    Task DeleteAsync(Guid id);

    // ── 习题关联 ──
    Task<List<KpExerciseLinkDto>> GetExerciseLinksAsync(Guid knowledgePointId);

    Task<KpExerciseLinkDto> AddExerciseLinkAsync(AddKpExerciseLinkInput input);

    Task<KpExerciseLinkDto> UpdateExerciseLinkAsync(UpdateKpExerciseLinkInput input);

    Task RemoveExerciseLinkAsync(Guid linkId);

    /// <summary>知识点图谱：先修关系（含跨课程前置）+ 合并关系，用于可视化与归一化决策</summary>
    Task<KnowledgePointGraphDto> GetGraphAsync(Guid courseId);

    // ── 归一化辅助 ──
    /// <summary>
    /// 合并建议（相似度 ≥ 阈值）。
    /// scope = course（同课程，默认）| tenant（跨课程，利用 pgvector 做全库 ANN 检索）。
    /// pgvector 不可用时回退进程内余弦 / 字符近似。
    /// </summary>
    Task<List<KpMergeSuggestionDto>> GetMergeSuggestionsAsync(Guid courseId, double threshold = 0.75, string scope = "course");

    /// <summary>向量引擎状态（pgvector / memory、维度、已入库向量数）</summary>
    Task<KnowledgeVectorStatusDto> GetVectorStatusAsync(Guid? courseId = null);

    /// <summary>为该课程知识点重建语义向量（未配置向量服务时不写入）</summary>
    Task<RebuildEmbeddingsResultDto> RebuildEmbeddingsAsync(Guid courseId);
}
