using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IPageIndexAppService : IApplicationService
{
    Task<ResourcePageIndexDto?> GetAsync(Guid resourceId);
    Task<List<PageIndexSearchResultDto>> SearchAsync(PageIndexSearchInput input);
    Task<ResourcePageIndexDto> GenerateAsync(Guid resourceVersionId);
}

public class PageIndexSearchInput
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 10;
}
