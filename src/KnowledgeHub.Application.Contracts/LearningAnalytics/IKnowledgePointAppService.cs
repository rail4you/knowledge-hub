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

    /// <summary>课程内所有习题及其关联的知识点（一题可对应多个知识点）</summary>
    Task<List<KpExerciseMappingDto>> GetExerciseMappingsAsync(Guid courseId);

    /// <summary>批量保存某道习题的知识点关联（可增可删，权重统一归一化）</summary>
    Task<KpExerciseMappingDto> SaveExerciseMappingsAsync(SaveKpExerciseLinksInput input);

    /// <summary>课程全部知识点（供「配置习题知识点」弹窗勾选）</summary>
    Task<List<KnowledgePointDetailDto>> GetCourseKnowledgePointsAsync(Guid courseId);

    /// <summary>
    /// 全量生成知识点：章节 → 知识点、习题 → 知识点关联一次跑完，并按需重建语义索引。
    /// 后台任务执行，返回提交结果。
    /// </summary>
    Task<KpGenerationResultDto> GenerateAsync(Guid courseId);

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
