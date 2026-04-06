using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IPageIndexService
{
    Task<ResourcePageIndexDto?> GeneratePageIndexAsync(Guid resourceVersionId);
    Task<ResourcePageIndexDto?> GetPageIndexAsync(Guid resourceId);
    Task<ResourcePageIndexDto?> GetPageIndexByVersionAsync(Guid resourceVersionId);
    Task<List<PageIndexSearchResultDto>> SearchPageIndexAsync(string query, int maxResults = 10);
}
