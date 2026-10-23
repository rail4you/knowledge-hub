using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Schools;

public interface ISchoolRepository : IRepository<School, Guid>
{
    /// <summary>按租户 Id 查找学校档案（1:1）。</summary>
    Task<School?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>按租户 Id 列表批量查找学校档案。</summary>
    Task<List<School>> GetByTenantIdsAsync(IEnumerable<Guid> tenantIds, CancellationToken cancellationToken = default);

    /// <summary>是否已为该租户建立学校档案。</summary>
    Task<bool> ExistsForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
