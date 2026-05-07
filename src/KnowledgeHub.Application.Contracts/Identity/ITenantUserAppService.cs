using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;

namespace KnowledgeHub.Application.Identity;

public interface ITenantUserAppService : IApplicationService
{
    Task<IdentityUserDto> CreateUserForTenantAsync(CreateTenantUserDto input);
    
    Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetTenantUsersInput input);
    
    Task<IdentityUserDto> GetAsync(Guid id);

    Task<List<string>> GetRolesForUserAsync(Guid userId);
    
    Task<IdentityUserDto> UpdateAsync(Guid id, UpdateTenantUserDto input);
    
    Task DeleteAsync(Guid id);
}
