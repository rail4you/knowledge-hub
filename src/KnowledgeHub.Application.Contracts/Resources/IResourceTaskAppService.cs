using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源任务：把媒体处理、文档索引、视频索引三类独立任务统一按「资源」聚合成嵌套表格，
/// 支持按资源分页与按任务独立重试。
/// </summary>
public interface IResourceTaskAppService : IApplicationService
{
    Task<PagedResultDto<ResourceTaskGroupDto>> GetGroupedListAsync(GetResourceTaskGroupsInput input);

    /// <summary>重试某个失败任务（kind: media | document-index | video-index）。</summary>
    Task RetryAsync(RetryTrackingTaskInput input);
}
