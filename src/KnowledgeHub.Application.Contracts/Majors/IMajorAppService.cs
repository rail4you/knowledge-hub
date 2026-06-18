using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Majors.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Majors;

public interface IMajorAppService : IApplicationService
{
    Task<MajorDto> GetAsync(Guid id);
    Task<PagedResultDto<MajorDto>> GetListAsync(PagedMajorRequestDto input);
    Task<List<MajorLookupDto>> GetLookupListAsync();
    Task<MajorDto> CreateAsync(CreateUpdateMajorDto input);
    Task<MajorDto> UpdateAsync(Guid id, CreateUpdateMajorDto input);
    Task DeleteAsync(Guid id);
}
