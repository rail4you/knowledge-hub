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

/// <summary>
/// 资源—专业关联仓储复用 <see cref="IRepository{TEntity, TKey}"/> 的标准增删改查，
/// 实际写入由 AppService 层在双写 <see cref="Resource.MajorId"/> 之后统一调用，
/// 保证主专业字段与关联表始终一致。
/// （参照 CourseMajor 的处理：使用通用 IRepository 而非自定义接口）
/// </summary>

