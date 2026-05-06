using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.News;

public interface INewsCommentAppService : IApplicationService
{
    Task<PagedResultDto<NewsCommentDto>> GetListAsync(PagedNewsCommentRequestDto input);
    Task<List<NewsCommentDto>> GetApprovedListByArticleAsync(Guid articleId);
    Task<NewsCommentDto> CreateAsync(CreateNewsCommentDto input);
    Task<NewsCommentDto> ReviewAsync(Guid id, ReviewNewsCommentDto input);
    Task DeleteAsync(Guid id);
}
