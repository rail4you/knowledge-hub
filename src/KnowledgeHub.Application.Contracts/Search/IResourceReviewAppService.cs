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
}
