using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Schools;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.Schools;

public class EfCoreSchoolRepository : EfCoreRepository<KnowledgeHubDbContext, School, Guid>, ISchoolRepository
{
    public EfCoreSchoolRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<School?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
    }

    public async Task<List<School>> GetByTenantIdsAsync(IEnumerable<Guid> tenantIds, CancellationToken cancellationToken = default)
    {
        var ids = tenantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<School>();
        }

        var query = await GetQueryableAsync();
        return await query.Where(x => ids.Contains(x.TenantId)).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.AnyAsync(x => x.TenantId == tenantId, cancellationToken);
    }
}
