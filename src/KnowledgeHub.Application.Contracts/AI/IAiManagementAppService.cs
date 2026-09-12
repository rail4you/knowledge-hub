using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// AI 使用管理（模型管理页“AI 使用管理”Tab）：Key 维护、用调用记录、配额配置。
/// 仅 AI.ManageTasks 权限（校级管理员 / host 超管）可访问。
/// </summary>
public interface IAiManagementAppService : IApplicationService
{
    /// <summary>当前 Key（脱敏）与模型、单价表。</summary>
    Task<AiManagementStatusDto> GetStatusAsync();

    /// <summary>更新 Qwen API Key（即时生效，无需重启）。</summary>
    Task UpdateApiKeyAsync(UpdateAiApiKeyDto input);

    /// <summary>用当前 Key 做一次最小调用，验证是否可用。</summary>
    Task<bool> TestConnectionAsync();

    /// <summary>用量记录分页 + 汇总。</summary>
    Task<PagedResultDto<AiUsageRecordDto>> GetUsageRecordsAsync(GetAiUsageRecordsInput input);

    Task<AiUsageSummaryDto> GetUsageSummaryAsync(GetAiUsageRecordsInput input);

    /// <summary>配额配置（角色 → 分组 → 每日次数）。</summary>
    Task<AiQuotasDto> GetQuotasAsync();

    Task UpdateQuotasAsync(AiQuotasDto input);
}
