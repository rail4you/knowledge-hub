using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Resources.Media;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Resources.Media;

/// <summary>
/// 资源媒体处理任务管理（后台任务跟踪/重试/取消 + 失败通知）。
/// </summary>
public interface IResourceMediaJobAppService : IApplicationService
{
    Task<PagedResultDto<ResourceMediaJobDto>> GetListAsync(GetResourceMediaJobsInput input);
    Task<ResourceMediaJobDto?> GetByResourceIdAsync(Guid resourceId);
    Task RetryAsync(Guid id);
    Task RetryAllFailedAsync();
    Task CancelAsync(Guid id);

    /// <summary>我的未读失败媒体任务数（驱动顶栏铃铛）。</summary>
    Task<int> GetMyUnreadCountAsync();
    /// <summary>我的失败/部分失败媒体任务（unreadOnly 时仅未读）。</summary>
    Task<List<ResourceMediaJobDto>> GetMyRecentAsync(bool unreadOnly = false);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
