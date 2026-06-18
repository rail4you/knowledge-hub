using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.TenantInfos;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.TenantInfos;

public class EfCoreTenantInfoRepository
    : EfCoreRepository<KnowledgeHubDbContext, TenantInfo, Guid>,
      ITenantInfoRepository
{
    public EfCoreTenantInfoRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<TenantInfo?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
    }
}
