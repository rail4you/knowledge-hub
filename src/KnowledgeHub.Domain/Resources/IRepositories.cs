using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Resources;

public interface IResourceRepository : IRepository<Resource, Guid>
{
    Task<List<Resource>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string sorting,
        string filter,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(
        string filter,
        CancellationToken cancellationToken = default);

    Task<Resource> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IResourceVersionRepository : IRepository<ResourceVersion, Guid>
{
    Task<List<ResourceVersion>> GetVersionsAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

public interface IResourceCategoryRepository : IRepository<ResourceCategory, Guid>
{
    Task<List<ResourceCategory>> GetListAsync(
        Guid? parentId = null,
        bool includeChildren = false,
        CancellationToken cancellationToken = default);
}

public interface IResourceAuditRepository : IRepository<ResourceAudit, Guid>
{
    Task<List<ResourceAudit>> GetAuditsAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

public interface IResourceCollectionRepository : IRepository<ResourceCollection, Guid>
{
    Task<bool> IsCollectedAsync(Guid resourceId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<ResourceCollection>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IPhysicalDeleteRequestRepository : IRepository<PhysicalDeleteRequest, Guid>
{
    Task<List<PhysicalDeleteRequest>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);
    Task<PhysicalDeleteRequest> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

public interface IResourceShareRepository : IRepository<ResourceShare, Guid>
{
    /// <summary>
    /// 获取资源被共享给了哪些租户（源租户视角，ABP 租户过滤器自动隔离源租户）。
    /// </summary>
    Task<List<ResourceShare>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取共享给指定目标租户的所有 ResourceShare（忽略 ABP 租户过滤器，按 TargetTenantId 字段做精确匹配）。
    /// </summary>
    Task<List<ResourceShare>> GetByTargetTenantAsync(Guid targetTenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断资源是否已经被共享给指定目标租户。
    /// </summary>
    Task<bool> ExistsAsync(Guid resourceId, Guid targetTenantId, CancellationToken cancellationToken = default);
}
