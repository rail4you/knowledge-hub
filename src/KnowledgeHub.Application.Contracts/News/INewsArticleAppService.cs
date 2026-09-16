using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.News;

public interface INewsArticleAppService : IApplicationService
{
    Task<NewsArticleDto> GetAsync(Guid id);
    /// <summary>跨租户只读预览：返回任意资源库已发布的资讯（不累加阅读数、不依赖当前租户登录态）。</summary>
    Task<NewsArticleDto> GetPublishedPreviewAsync(Guid id);
    Task<PagedResultDto<NewsArticleDto>> GetListAsync(PagedNewsArticleRequestDto input);
    Task<PagedResultDto<NewsArticleDto>> GetPublishedListAsync(PagedNewsArticleRequestDto input);
    Task<List<NewsArticleDto>> GetHotListAsync(int maxCount = 10);
    Task<NewsArticleDto> CreateAsync(CreateUpdateNewsArticleDto input);
    Task<NewsArticleDto> UpdateAsync(Guid id, CreateUpdateNewsArticleDto input);
    Task DeleteAsync(Guid id);
    Task SubmitForReviewAsync(Guid id);
    Task PublishAsync(Guid id);
    Task RejectAsync(Guid id, ReviewNewsArticleDto input);
    Task ArchiveAsync(Guid id, ReviewNewsArticleDto input);
    Task LikeAsync(Guid id);
}
