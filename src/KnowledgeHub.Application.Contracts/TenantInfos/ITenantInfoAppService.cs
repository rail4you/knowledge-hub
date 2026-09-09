using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.TenantInfos.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.TenantInfos;

public interface ITenantInfoAppService : IApplicationService
{
    /// <summary>获取租户关联的展示信息：host 返回所有租户，租户管理员仅返回本租户</summary>
    Task<List<TenantInfoListItemDto>> GetListAsync();

    /// <summary>获取当前租户的信息</summary>
    Task<TenantInfoDto> GetCurrentAsync();

    /// <summary>获取指定租户的信息</summary>
    Task<TenantInfoDto> GetByTenantIdAsync(Guid tenantId);

    /// <summary>创建或更新当前租户的信息</summary>
    Task<TenantInfoDto> SaveCurrentAsync(CreateUpdateTenantInfoDto input);

    /// <summary>创建或更新指定租户的信息：host 可修改任意租户，租户管理员仅可修改本租户</summary>
    Task<TenantInfoDto> SaveByTenantIdAsync(Guid tenantId, CreateUpdateTenantInfoDto input);

    /// <summary>获取租户首页知识图谱</summary>
    Task<TenantKnowledgeGraphDto> GetKnowledgeGraphAsync(Guid tenantId);

    /// <summary>获取当前租户知识图谱</summary>
    Task<TenantKnowledgeGraphDto> GetCurrentKnowledgeGraphAsync();
}
