using System.Threading.Tasks;
using KnowledgeHub.Application.Edition;
using KnowledgeHub.Install.Dto;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("app")]
[Route("api/app/edition")]
public class EditionController : AbpControllerBase
{
    private readonly IEditionAppService _editionAppService;

    public EditionController(IEditionAppService editionAppService)
    {
        _editionAppService = editionAppService;
    }

    [HttpGet]
    [Route("current")]
    public async Task<EditionDto> GetCurrentEditionAsync()
    {
        return await _editionAppService.GetCurrentEditionAsync();
    }

    [HttpPost]
    [Route("upgrade")]
    public async Task UpgradeToStandardAsync([FromBody] EditionUpgradeInputDto input)
    {
        await _editionAppService.UpgradeToStandardAsync(input);
    }
}