using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.TeachingAgents;

public interface ITeachingAgentAppService : IApplicationService
{
    Task<List<TeachingAgentPresetDto>> GetPresetsAsync();
    Task<TeachingAgentDto> CreateDraftAsync(CreateUpdateTeachingAgentDto input);
    Task<TeachingAgentDto> UpdateDraftAsync(Guid id, CreateUpdateTeachingAgentDto input);
    Task<TeachingAgentDto> PublishVersionAsync(Guid id, PublishTeachingAgentVersionDto input);
    Task<PagedResultDto<TeachingAgentDto>> GetListAsync(PagedTeachingAgentRequestDto input);
    Task<TeachingAgentDetailDto> GetDetailAsync(Guid id);
    Task<TeachingAgentDto> CloneFromPresetAsync(CloneTeachingAgentFromPresetDto input);
}
