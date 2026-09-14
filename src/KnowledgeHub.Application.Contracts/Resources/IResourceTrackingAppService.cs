using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源全链路任务跟踪：聚合索引任务、媒体任务（缩略图/预览）、院校/联盟审核记录，
/// 以时间轴方式展示资源从上传到发布审核的完整链路，并支持按任务独立重试。
/// </summary>
public interface IResourceTrackingAppService : IApplicationService
{
    /// <summary>资源列表（左侧面板）。</summary>
    Task<PagedResultDto<ResourceTrackingResourceDto>> GetListAsync(GetTrackingResourcesInput input);

    /// <summary>单个资源的全链路时间轴。</summary>
    Task<ResourceTrackingTimelineDto> GetTimelineAsync(Guid resourceId);
}
