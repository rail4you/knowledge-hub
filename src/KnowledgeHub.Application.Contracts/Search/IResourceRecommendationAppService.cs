using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IResourceRecommendationAppService : IApplicationService
{
    Task<List<RecommendedResourceDto>> GetPersonalizedRecommendationsAsync(int count = 10);
    Task<List<RecommendedResourceDto>> GetRelatedResourcesAsync(Guid resourceId, int count = 6);
    Task<ResourceStatisticsDto> GetResourceStatisticsAsync(Guid resourceId);
    Task<List<RecommendedResourceDto>> GetTrendingResourcesAsync(int count = 10);
    Task<List<RecommendedResourceDto>> GetCategoryBasedRecommendationsAsync(int count = 6);
}
