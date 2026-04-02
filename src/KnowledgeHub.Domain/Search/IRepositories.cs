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

public interface IDocumentIndexingJobRepository : IRepository<DocumentIndexingJob, Guid>
{
    Task<DocumentIndexingJob?> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<DocumentIndexingJob?> GetPendingJobByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<List<DocumentIndexingJob>> GetPendingJobsAsync(int maxResultCount, CancellationToken cancellationToken = default);
    Task<List<DocumentIndexingJob>> GetFailedJobsForRetryAsync(int maxResultCount, CancellationToken cancellationToken = default);
    Task<List<DocumentIndexingJob>> GetByStatusAsync(IndexingJobStatus status, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
}

public interface IPageContentRepository : IRepository<PageContent, Guid>
{
    Task<List<PageContent>> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<PageContent?> GetByResourceAndPageAsync(Guid resourceId, int pageNumber, CancellationToken cancellationToken = default);
    Task DeleteByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

public interface IResourceReviewRepository : IRepository<ResourceReview, Guid>
{
    Task<ResourceReview?> GetByUserAndResourceAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken = default);
    Task<List<ResourceReview>> GetByResourceIdAsync(Guid resourceId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    Task<int> GetCountByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

public interface IVideoIndexingJobRepository : IRepository<VideoIndexingJob, Guid>
{
    Task<VideoIndexingJob?> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<VideoIndexingJob?> GetPendingJobByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<List<VideoIndexingJob>> GetPendingJobsAsync(int maxResultCount, CancellationToken cancellationToken = default);
    Task<List<VideoIndexingJob>> GetFailedJobsForRetryAsync(int maxResultCount, CancellationToken cancellationToken = default);
    Task<List<VideoIndexingJob>> GetByStatusAsync(VideoIndexingJobStatus status, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
}
