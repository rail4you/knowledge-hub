using System;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Resources.Media;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Resources.Media;

/// <summary>
/// 资源媒体处理任务管理（后台任务跟踪/重试/取消）。
/// </summary>
public interface IResourceMediaJobAppService : IApplicationService
{
    Task<PagedResultDto<ResourceMediaJobDto>> GetListAsync(GetResourceMediaJobsInput input);
    Task<ResourceMediaJobDto?> GetByResourceIdAsync(Guid resourceId);
    Task RetryAsync(Guid id);
    Task RetryAllFailedAsync();
    Task CancelAsync(Guid id);
}
