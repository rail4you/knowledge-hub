using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Majors;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.Majors;

public class EfCoreMajorRepository : EfCoreRepository<KnowledgeHubDbContext, Major, Guid>, IMajorRepository
{
    public EfCoreMajorRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Major?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var query = await GetQueryableAsync();
        return await query
            .FirstOrDefaultAsync(x => x.Name == trimmed, cancellationToken);
    }

    public async Task<Major?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var query = await GetQueryableAsync();
        return await query
            .FirstOrDefaultAsync(x => x.Code == trimmed, cancellationToken);
    }

    public async Task<List<Major>> GetLookupListAsync(CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}
