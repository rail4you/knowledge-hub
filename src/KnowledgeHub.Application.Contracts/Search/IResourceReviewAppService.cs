using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IResourceReviewAppService : IApplicationService
{
    Task<ResourceReviewDto> CreateAsync(CreateResourceReviewDto input);
    Task<ResourceReviewDto> UpdateAsync(Guid id, UpdateResourceReviewDto input);
    Task DeleteAsync(Guid id);
    Task<ResourceReviewDto?> GetMyReviewAsync(Guid resourceId);
    Task<List<ResourceReviewDto>> GetResourceReviewsAsync(Guid resourceId, int skipCount = 0, int maxResultCount = 20);
    Task<ResourceRatingSummaryDto> GetRatingSummaryAsync(Guid resourceId);

    /// <summary>批量获取多个资源的评分统计，避免列表页 N+1 请求。</summary>
    Task<List<ResourceRatingSummaryDto>> GetRatingSummariesAsync(List<Guid> resourceIds);
}
