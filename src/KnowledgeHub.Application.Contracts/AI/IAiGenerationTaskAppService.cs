using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// AI 异步生成任务：创建后立即返回，后台作业执行，前端轮询进度 / 结果 / 通知。
/// </summary>
public interface IAiGenerationTaskAppService : IApplicationService
{
    /// <summary>创建任务并入队后台作业，立即返回。</summary>
    Task<AiGenerationTaskDto> CreateAsync(CreateAiGenerationTaskDto input);

    /// <summary>任务列表（监控面板 / 各功能结果表）。无管理权限时只能看到自己的任务。</summary>
    Task<PagedResultDto<AiGenerationTaskDto>> GetListAsync(GetAiGenerationTaskListDto input);

    /// <summary>任务详情（含 InputJson / ResultJson）。</summary>
    Task<AiGenerationTaskDto> GetAsync(Guid id);

    /// <summary>仅获取结果 JSON，避免大字段走列表。</summary>
    Task<string?> GetResultAsync(Guid id);

    /// <summary>取消（Pending / Running）。运行中的任务为协作式取消，在下一步生效。</summary>
    Task CancelAsync(Guid id);

    /// <summary>重试（仅 Failed / Cancelled / Completed 可重试）。</summary>
    Task RetryAsync(Guid id);

    Task DeleteAsync(Guid id);

    /// <summary>当前用户未读的已完成任务数（顶部铃铛）。</summary>
    Task<int> GetMyUnreadCountAsync();

    /// <summary>当前用户已完成的任务（可选仅未读），按完成时间倒序，用于通知下拉。</summary>
    Task<List<AiGenerationTaskDto>> GetMyCompletedAsync(bool unreadOnly = false);

    Task MarkAsReadAsync(Guid id);

    Task MarkAllAsReadAsync();
}
