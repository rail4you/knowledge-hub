using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Domain.Search.Enums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore;

public class EfCoreDocumentIndexRepository : EfCoreRepository<KnowledgeHubDbContext, DocumentIndex, Guid>, IDocumentIndexRepository
{
    public EfCoreDocumentIndexRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<DocumentIndex>> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(x => x.ResourceId == resourceId)
            .OrderBy(x => x.PageNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentIndex?> GetByResourceAndPageAsync(Guid resourceId, int pageNumber, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .FirstOrDefaultAsync(x => x.ResourceId == resourceId && x.PageNumber == pageNumber, cancellationToken);
    }

    public async Task<List<DocumentIndex>> GetByStatusAsync(IndexStatus status, int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(x => x.IndexStatus == status)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountByStatusAsync(IndexStatus status, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .CountAsync(x => x.IndexStatus == status, cancellationToken);
    }

    public async Task<List<DocumentIndex>> GetPendingIndicesAsync(int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(x => x.IndexStatus == IndexStatus.Pending)
            .OrderBy(x => x.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }
}
