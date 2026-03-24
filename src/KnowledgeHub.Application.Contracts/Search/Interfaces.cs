using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IMeiliSearchService : IApplicationService
{
    Task<IndexTaskResultDto> IndexDocumentAsync(Guid resourceId);
    Task<IndexTaskResultDto> IndexAllPagesAsync(Guid resourceId);
    Task<SearchResultDto> SearchAsync(SearchQueryDto query);
    Task<SearchResultDto> HybridSearchAsync(HybridSearchQueryDto query);
    Task DeleteDocumentAsync(Guid resourceId);
    Task<IndexStatusDto?> GetIndexingTaskStatusAsync(long taskId);
    Task<List<IndexStatusDto>> GetPendingIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20);
    Task<List<IndexStatusDto>> GetAllIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20);
    Task RetryFailedIndexingAsync(Guid documentIndexId);
    Task EnsureIndexExistsAsync();
}

public interface IDocumentExtractionService : IApplicationService
{
    Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId);
}

public class PageContentDto
{
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
}

public interface IEmbeddingService : IApplicationService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<List<float>> GenerateBatchEmbeddingsAsync(List<string> texts);
    bool IsConfigured { get; }
}

public interface ISearchAnalyticsService : IApplicationService
{
    Task LogSearchAsync(Guid userId, string query, int searchType, int resultCount, string? filters);
    Task LogResourceViewAsync(LogViewDto input);
    Task<SearchStatsDto> GetSearchStatsAsync(DateTime? startDate, DateTime? endDate);
    Task<List<PopularSearchDto>> GetPopularSearchesAsync(int count = 10);
    Task<List<TopResourceDto>> GetTopResourcesAsync(int count = 10);
    Task<List<SearchHistoryDto>> GetUserSearchHistoryAsync(Guid userId, int skipCount = 0, int maxResultCount = 20);
}

public interface ISearchAppService : IApplicationService
{
    Task<SearchResultDto> SearchAsync(SearchQueryDto input);
    Task<SearchResultDto> HybridSearchAsync(HybridSearchQueryDto input);
    Task<IndexTaskResultDto> IndexResourceAsync(IndexDocumentDto input);
    Task<IndexTaskResultDto> RefreshDocumentIndexAsync(RefreshDocumentIndexDto input);
    Task DeleteIndexAsync(Guid resourceId);
    Task<List<IndexStatusDto>> GetIndexingTasksAsync(int skipCount = 0, int maxResultCount = 20);
    Task<IndexStatusDto?> GetIndexTaskStatusAsync(long taskId);
    Task LogViewAsync(LogViewDto input);
    Task<List<SearchHistoryDto>> GetMySearchHistoryAsync(int skipCount = 0, int maxResultCount = 20);
    Task<SearchStatsDto> GetSearchStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<List<PopularSearchDto>> GetPopularSearchesAsync(int count = 10);
    Task<List<TopResourceDto>> GetTopResourcesAsync(int count = 10);
}
