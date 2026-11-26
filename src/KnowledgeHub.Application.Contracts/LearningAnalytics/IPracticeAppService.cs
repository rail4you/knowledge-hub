using System;
using System.Threading.Tasks;
using KnowledgeHub.LearningAnalytics.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 学生端专项练习：按推荐引擎给出的薄弱知识点取题 → 作答（复用在线作答记录）→ 回写掌握度。
/// 路由前缀（ABP 约定）：<c>/api/app/practice</c>。
/// </summary>
public interface IPracticeAppService : IApplicationService
{
    /// <summary>取专项练习题集（knowledgePoint 为空时取最薄弱知识点）</summary>
    /// <summary>
    /// 取练习套题。
    /// <paramref name="knowledgePoint"/> = 智学分析知识点（推荐引擎口径）；
    /// <paramref name="knowledgeResourceId"/> = 课程知识点树节点（视频「练一练」等直连场景，优先级更高）。
    /// </summary>
    Task<PracticeSetDto> GetSetAsync(
        Guid courseId,
        Guid? knowledgePoint = null,
        int take = 6,
        Guid? knowledgeResourceId = null);

    /// <summary>提交练习并回写掌握度</summary>
    Task<PracticeResultDto> SubmitAsync(SubmitPracticeInput input);
}
