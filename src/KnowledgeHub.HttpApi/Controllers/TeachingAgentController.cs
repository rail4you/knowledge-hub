using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents;
using KnowledgeHub.TeachingAgents.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("teaching")]
[Route("api/teaching-agents")]
public class TeachingAgentController : AbpControllerBase
{
    private readonly ITeachingAgentAppService _teachingAgentAppService;

    public TeachingAgentController(ITeachingAgentAppService teachingAgentAppService)
    {
        _teachingAgentAppService = teachingAgentAppService;
    }

    [HttpGet("presets")]
    public Task<List<TeachingAgentPresetDto>> GetPresetsAsync()
    {
        return _teachingAgentAppService.GetPresetsAsync();
    }

    [HttpGet]
    public Task<PagedResultDto<TeachingAgentDto>> GetListAsync([FromQuery] PagedTeachingAgentRequestDto input)
    {
        return _teachingAgentAppService.GetListAsync(input);
    }

    [HttpGet("{id:guid}")]
    public Task<TeachingAgentDetailDto> GetAsync(Guid id)
    {
        return _teachingAgentAppService.GetDetailAsync(id);
    }

    [HttpPost]
    public Task<TeachingAgentDto> CreateAsync([FromBody] CreateUpdateTeachingAgentDto input)
    {
        return _teachingAgentAppService.CreateDraftAsync(input);
    }

    [HttpPut("{id:guid}")]
    public Task<TeachingAgentDto> UpdateAsync(Guid id, [FromBody] CreateUpdateTeachingAgentDto input)
    {
        return _teachingAgentAppService.UpdateDraftAsync(id, input);
    }

    [HttpPost("{id:guid}/publish")]
    public Task<TeachingAgentDto> PublishAsync(Guid id, [FromBody] PublishTeachingAgentVersionDto input)
    {
        return _teachingAgentAppService.PublishVersionAsync(id, input);
    }

    [HttpPost("from-preset")]
    public Task<TeachingAgentDto> CloneFromPresetAsync([FromBody] CloneTeachingAgentFromPresetDto input)
    {
        return _teachingAgentAppService.CloneFromPresetAsync(input);
    }
}
