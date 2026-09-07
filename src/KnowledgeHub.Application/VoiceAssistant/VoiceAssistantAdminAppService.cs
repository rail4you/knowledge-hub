using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Features;
using KnowledgeHub.Permissions;
using KnowledgeHub.VoiceAssistant;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.FeatureManagement;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Application.VoiceAssistant;

/// <summary>
/// 语音助手管理：按租户读写 KnowledgeHub.VoiceAssistant Feature（"T" provider）。
/// 未显式设置时走定义默认值（true，即显示）。仅 host 全局管理员可调。
/// </summary>
[Authorize(KnowledgeHubPermissions.VoiceAssistant.Manage)]
public class VoiceAssistantAdminAppService : KnowledgeHubAppService, IVoiceAssistantAdminAppService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IFeatureManager _featureManager;

    public VoiceAssistantAdminAppService(
        ITenantRepository tenantRepository,
        IFeatureManager featureManager)
    {
        _tenantRepository = tenantRepository;
        _featureManager = featureManager;
    }

    public async Task<List<VoiceAssistantTenantStateDto>> GetTenantStatesAsync()
    {
        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<VoiceAssistantTenantStateDto>();
        foreach (var t in tenants)
        {
            var value = await _featureManager.GetOrNullAsync(KnowledgeHubFeatures.VoiceAssistant, "T", t.Id.ToString());
            // 未显式设置 → 定义默认值 true（显示）
            var enabled = value == null
                ? true
                : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            result.Add(new VoiceAssistantTenantStateDto
            {
                TenantId = t.Id,
                TenantName = t.Name,
                Enabled = enabled
            });
        }
        return result;
    }

    public async Task SetTenantEnabledAsync(SetVoiceAssistantTenantEnabledDto input)
    {
        await _featureManager.SetAsync(
            KnowledgeHubFeatures.VoiceAssistant,
            input.Enabled ? "true" : "false",
            "T",
            input.TenantId.ToString());
    }
}
