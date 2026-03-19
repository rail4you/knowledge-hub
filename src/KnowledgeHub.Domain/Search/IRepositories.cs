using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using KnowledgeHub.Domain.Search.Enums;

namespace KnowledgeHub.Domain.Search;

public interface IDocumentIndexRepository : IRepository<DocumentIndex, Guid>
{
    Task<List<DocumentIndex>> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<DocumentIndex?> GetByResourceAndPageAsync(Guid resourceId, int pageNumber, CancellationToken cancellationToken = default);
    Task<List<DocumentIndex>> GetByStatusAsync(IndexStatus status, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    Task<long> GetCountByStatusAsync(IndexStatus status, CancellationToken cancellationToken = default);
    Task<List<DocumentIndex>> GetPendingIndicesAsync(int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
}

public interface ISearchQueryRepository : IRepository<SearchQuery, Guid>
{
    Task<List<SearchQuery>> GetByUserIdAsync(Guid userId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    Task<List<SearchQuery>> GetRecentQueriesAsync(int count, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetPopularSearchTermsAsync(int count, CancellationToken cancellationToken = default);
}

public interface IResourceViewLogRepository : IRepository<ResourceViewLog, Guid>
{
    Task<List<ResourceViewLog>> GetByResourceIdAsync(Guid resourceId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    Task<List<ResourceViewLog>> GetByUserIdAsync(Guid userId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    Task<int> GetViewCountByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

public interface ISearchStatisticsRepository : IRepository<SearchStatistics, Guid>
{
    Task<SearchStatistics?> GetByDateAsync(Guid organizationId, DateTime date, CancellationToken cancellationToken = default);
    Task<List<SearchStatistics>> GetByDateRangeAsync(Guid organizationId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

public interface IResourceExposureRepository : IRepository<ResourceExposure, Guid>
{
    Task<ResourceExposure?> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<List<ResourceExposure>> GetTopExposedResourcesAsync(int count, CancellationToken cancellationToken = default);
    Task IncrementExposureAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task IncrementClickAsync(Guid resourceId, CancellationToken cancellationToken = default);
}
