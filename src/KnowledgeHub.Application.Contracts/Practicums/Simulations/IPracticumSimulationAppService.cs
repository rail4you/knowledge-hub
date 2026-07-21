using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Practicums.Simulations;

public interface IPracticumSimulationAppService : IApplicationService
{
    Task<List<PracticumSimulationDto>> GetListByProjectAsync(Guid projectId);
    Task<List<PracticumSimulationDto>> GetAllAsync();
    Task<PracticumSimulationDto> CreateAsync(CreatePracticumSimulationDto input);
    Task<PracticumSimulationDto> UpdateAsync(Guid id, UpdatePracticumSimulationDto input);
    Task DeleteAsync(Guid id);
}
