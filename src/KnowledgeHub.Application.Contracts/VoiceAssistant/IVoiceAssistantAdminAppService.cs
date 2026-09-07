using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.VoiceAssistant;

/// <summary>
/// 语音助手管理：host 全局管理员按租户开/关 KnowledgeHub.VoiceAssistant Feature。
/// 常规 Controller 自动生成：GET /api/app/voice-assistant-admin/tenant-states，
/// POST /api/app/voice-assistant-admin/set-tenant-enabled。
/// </summary>
public interface IVoiceAssistantAdminAppService : IApplicationService
{
    Task<List<VoiceAssistantTenantStateDto>> GetTenantStatesAsync();
    Task SetTenantEnabledAsync(SetVoiceAssistantTenantEnabledDto input);
}

public class VoiceAssistantTenantStateDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class SetVoiceAssistantTenantEnabledDto
{
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
}
