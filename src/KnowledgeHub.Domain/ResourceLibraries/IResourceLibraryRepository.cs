using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.ResourceLibraries;

public interface IResourceLibraryRepository : IRepository<ResourceLibrary, Guid>
{
    /// <summary>获取指定资源库的成员列表（跨租户：成员学校可能不是库归属租户）。</summary>
    Task<List<ResourceLibraryMember>> GetMembersAsync(Guid libraryId, CancellationToken cancellationToken = default);

    /// <summary>获取某学校参与共建的全部资源库（含作为主建学校与作为合作学校的）。</summary>
    Task<List<ResourceLibrary>> GetByMemberTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>判断某学校是否为指定资源库的成员。</summary>
    Task<bool> IsMemberAsync(Guid libraryId, Guid tenantId, CancellationToken cancellationToken = default);
}
