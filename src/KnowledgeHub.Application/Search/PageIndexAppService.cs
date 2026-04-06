using System;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.Search;

public class PageIndexAppService : KnowledgeHubAppService, IPageIndexAppService, ITransientDependency
{
    private readonly IPageIndexService _pageIndexService;

    public PageIndexAppService(IPageIndexService pageIndexService)
    {
        _pageIndexService = pageIndexService;
    }

    public async Task<ResourcePageIndexDto?> GetAsync(Guid resourceId)
    {
        return await _pageIndexService.GetPageIndexAsync(resourceId);
    }

    public async Task<System.Collections.Generic.List<PageIndexSearchResultDto>> SearchAsync(PageIndexSearchInput input)
    {
        return await _pageIndexService.SearchPageIndexAsync(input.Query, input.MaxResults);
    }

    public async Task<ResourcePageIndexDto> GenerateAsync(Guid resourceVersionId)
    {
        var result = await _pageIndexService.GeneratePageIndexAsync(resourceVersionId);
        return result ?? throw new Volo.Abp.BusinessException("KnowledgeHub:PageIndexGenerationFailed")
            .WithData("ResourceVersionId", resourceVersionId);
    }
}
