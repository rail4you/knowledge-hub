using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.TenantInfos;

public interface ITenantInfoRepository : IRepository<TenantInfo, Guid>
{
    Task<TenantInfo?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
