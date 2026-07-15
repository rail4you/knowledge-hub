using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace KnowledgeHub.Controllers;

[Area("learning")]
[Route("api/learning/ai/summary")]
[DisableAuditing]
[IgnoreAntiforgeryToken]
public class SummaryGenerationController : AbpControllerBase
{
    private readonly ISummaryGenerationAppService _service;

    public SummaryGenerationController(ISummaryGenerationAppService service)
    {
        _service = service;
    }

    [HttpPost("generate")]
    public async Task<GenerateSummaryResultDto> Generate([FromBody] GenerateSummaryInputDto input)
    {
        return await _service.GenerateForResourceAsync(input);
    }

    [HttpPost("batch-generate")]
    public async Task<BatchGenerateSummaryResultDto> BatchGenerate([FromBody] BatchGenerateSummaryInputDto input)
    {
        return await _service.BatchGenerateAsync(input);
    }
}