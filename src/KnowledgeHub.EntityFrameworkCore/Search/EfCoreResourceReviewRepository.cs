using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Domain.Search;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.Search;

public class EfCoreResourceReviewRepository : EfCoreRepository<KnowledgeHubDbContext, ResourceReview, Guid>, IResourceReviewRepository
{
    public EfCoreResourceReviewRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<ResourceReview?> GetByUserAndResourceAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(r => r.UserId == userId && r.ResourceId == resourceId, cancellationToken);
    }

    public async Task<List<ResourceReview>> GetByResourceIdAsync(Guid resourceId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(r => r.CreationTime)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.CountAsync(r => r.ResourceId == resourceId, cancellationToken);
    }
}
