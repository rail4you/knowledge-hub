using System;
using System.Threading.Tasks;
using KnowledgeHub.TenantInfos.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.TenantInfos;

public interface ITenantInfoAppService : IApplicationService
{
    /// <summary>获取当前租户的信息</summary>
    Task<TenantInfoDto> GetCurrentAsync();

    /// <summary>获取指定租户的信息</summary>
    Task<TenantInfoDto> GetByTenantIdAsync(Guid tenantId);

    /// <summary>创建或更新当前租户的信息</summary>
    Task<TenantInfoDto> SaveCurrentAsync(CreateUpdateTenantInfoDto input);

    /// <summary>创建或更新指定租户的信息（host 管理员使用）</summary>
    Task<TenantInfoDto> SaveByTenantIdAsync(Guid tenantId, CreateUpdateTenantInfoDto input);

    /// <summary>获取租户首页知识图谱</summary>
    Task<TenantKnowledgeGraphDto> GetKnowledgeGraphAsync(Guid tenantId);

    /// <summary>获取当前租户知识图谱</summary>
    Task<TenantKnowledgeGraphDto> GetCurrentKnowledgeGraphAsync();
}
