using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Users;

public interface IUserAppService : IApplicationService
{
    Task<UserDto> GetAsync(Guid id);

    Task<PagedResultDto<UserDto>> GetListAsync(GetUserListDto input);

    Task<UserDto> CreateAsync(CreateUserDto input);

    Task UpdateAsync(Guid id, UpdateUserDto input);

    Task DeleteAsync(Guid id);
}
