using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Identity;

public interface ITenantUserAppService : IApplicationService
{
    Task<TenantUserDto> CreateUserForTenantAsync(CreateTenantUserDto input);

    Task<PagedResultDto<TenantUserDto>> GetListAsync(GetTenantUsersInput input);

    Task<TenantUserDto> GetAsync(Guid id);

    Task<List<string>> GetRolesForUserAsync(Guid userId);

    Task<TenantUserDto> UpdateAsync(Guid id, UpdateTenantUserDto input);

    Task DeleteAsync(Guid id);
}
