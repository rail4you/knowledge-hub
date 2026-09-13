using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Workbench;

/// <summary>
/// 系统工作台：聚合各管理模块的统计信息，作为管理端首页数据源。
/// </summary>
public interface IWorkbenchAppService : IApplicationService
{
    /// <summary>
    /// 获取工作台统计。
    /// 租户上下文用户固定返回当前租户；host 全局管理员可通过 input.TenantId
    /// 切换查看指定租户，或传空汇总全部租户。
    /// </summary>
    Task<WorkbenchStatsDto> GetStatsAsync(WorkbenchQueryDto input);
}
