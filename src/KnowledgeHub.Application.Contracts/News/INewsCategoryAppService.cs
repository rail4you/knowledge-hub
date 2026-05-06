using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.News;

public interface INewsCategoryAppService : IApplicationService
{
    Task<NewsCategoryDto> GetAsync(Guid id);
    Task<PagedResultDto<NewsCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<List<NewsCategoryDto>> GetActiveListAsync();
    Task<List<NewsCategoryDto>> GetTreeAsync();
    Task<NewsCategoryDto> CreateAsync(CreateUpdateNewsCategoryDto input);
    Task<NewsCategoryDto> UpdateAsync(Guid id, CreateUpdateNewsCategoryDto input);
    Task DeleteAsync(Guid id);
}
