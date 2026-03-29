using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.Resources;

public class EfCoreResourceRepository : EfCoreRepository<KnowledgeHubDbContext, Resource, Guid>, IResourceRepository
{
    public EfCoreResourceRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<Resource>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string sorting,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var query = await ApplyFilterAsync(skipCount, maxResultCount, sorting, filter);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountAsync(string filter, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Name.Contains(filter));
        }
        
        return await query.LongCountAsync(cancellationToken);
    }

    public async Task<Resource> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        
        return await query
            .Include(x => x.Versions)
            .Include(x => x.Audits)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException(typeof(Resource), id);
    }

    private async Task<IQueryable<Resource>> ApplyFilterAsync(
        int skipCount,
        int maxResultCount,
        string sorting,
        string filter)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Name.Contains(filter));
        }

        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? "CreationTime DESC" : sorting);

        return query.Skip(skipCount).Take(maxResultCount);
    }
}

public class EfCoreResourceVersionRepository : EfCoreRepository<KnowledgeHubDbContext, ResourceVersion, Guid>, IResourceVersionRepository
{
    public EfCoreResourceVersionRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<ResourceVersion>> GetVersionsAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.ResourceId == resourceId)
            .OrderByDescending(x => x.CreationTime)
            .ToListAsync(cancellationToken);
    }
}

public class EfCoreResourceCategoryRepository : EfCoreRepository<KnowledgeHubDbContext, ResourceCategory, Guid>, IResourceCategoryRepository
{
    public EfCoreResourceCategoryRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<ResourceCategory>> GetListAsync(
        Guid? parentId = null,
        bool includeChildren = false,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        
        if (parentId.HasValue)
        {
            query = query.Where(x => x.ParentId == parentId);
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreationTime)
            .ToListAsync(cancellationToken);
    }
}

public class EfCoreResourceAuditRepository : EfCoreRepository<KnowledgeHubDbContext, ResourceAudit, Guid>, IResourceAuditRepository
{
    public EfCoreResourceAuditRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<ResourceAudit>> GetAuditsAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.ResourceId == resourceId)
            .OrderByDescending(x => x.CreationTime)
            .ToListAsync(cancellationToken);
    }
}

public class EfCoreResourceCollectionRepository : EfCoreRepository<KnowledgeHubDbContext, ResourceCollection, Guid>, IResourceCollectionRepository
{
    public EfCoreResourceCollectionRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<bool> IsCollectedAsync(Guid resourceId, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.AnyAsync(x => x.ResourceId == resourceId && x.UserId == userId, cancellationToken);
    }

    public async Task<List<ResourceCollection>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreationTime)
            .ToListAsync(cancellationToken);
    }
}

public class EfCorePhysicalDeleteRequestRepository : EfCoreRepository<KnowledgeHubDbContext, PhysicalDeleteRequest, Guid>, IPhysicalDeleteRequestRepository
{
    public EfCorePhysicalDeleteRequestRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<PhysicalDeleteRequest>> GetPendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.Status == PhysicalDeleteStatus.Pending)
            .OrderByDescending(x => x.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<PhysicalDeleteRequest> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(x => x.ResourceId == resourceId, cancellationToken);
    }
}
