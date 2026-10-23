using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Schools.Dtos;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库 AppService（多校共建）。
///
/// <para>
/// 需求「资源库按专业维度显示，资源库可以多校共建，体现主建学校（专业），和合作学校」的服务端契约。
/// </para>
/// </summary>
public interface IResourceLibraryAppService
{
    /// <summary>获取资源库列表（当前租户可见：本租户主建的 + 本租户参与合作的）。</summary>
    Task<List<ResourceLibraryDto>> GetListAsync(ResourceLibraryListRequestDto input);

    /// <summary>获取单个资源库详情（含成员列表）。</summary>
    Task<ResourceLibraryDto> GetAsync(Guid id);

    /// <summary>创建资源库（当前租户自动成为主建学校）。</summary>
    Task<ResourceLibraryDto> CreateAsync(CreateResourceLibraryDto input);

    /// <summary>更新资源库（仅主建学校可操作）。</summary>
    Task<ResourceLibraryDto> UpdateAsync(Guid id, UpdateResourceLibraryDto input);

    /// <summary>删除资源库（仅主建学校可操作）。</summary>
    Task DeleteAsync(Guid id);

    /// <summary>添加合作学校（仅主建学校可操作）。</summary>
    Task<ResourceLibraryMemberDto> AddMemberAsync(Guid id, AddResourceLibraryMemberDto input);

    /// <summary>移除合作学校（仅主建学校可操作）。</summary>
    Task RemoveMemberAsync(Guid id, Guid memberTenantId);
}
