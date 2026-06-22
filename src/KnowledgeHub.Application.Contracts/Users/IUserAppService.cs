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

    /// <summary>
    /// 当前登录用户的个人资料（供学生端修改联系方式）
    /// </summary>
    Task<MyProfileDto> GetMyProfileAsync();

    /// <summary>
    /// 当前登录用户更新联系方式（仅允许改 Email/Phone）
    /// </summary>
    Task UpdateMyProfileAsync(UpdateMyProfileDto input);
}
