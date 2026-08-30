using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Resources;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.Resources;

public class EfCoreResourceShareRepository : EfCoreRepository<KnowledgeHubDbContext, ResourceShare, Guid>, IResourceShareRepository
{
    public EfCoreResourceShareRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<ResourceShare>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.ResourceId == resourceId)
            .OrderByDescending(x => x.SharedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询共享给某个目标租户的所有 ResourceShare。
    /// 共享记录按源租户隔离存储，但跨租户的目标租户查询必须禁用租户过滤器，
    /// 否则 ABP 的 IMultiTenant 过滤器会把跨租户的 ResourceShare 全部过滤掉。
    /// </summary>
    public async Task<List<ResourceShare>> GetByTargetTenantAsync(Guid targetTenantId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.TargetTenantId == targetTenantId)
            .OrderByDescending(x => x.SharedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid resourceId, Guid targetTenantId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.AnyAsync(x => x.ResourceId == resourceId && x.TargetTenantId == targetTenantId, cancellationToken);
    }
}
