using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Users;

public interface IUserAppService : IApplicationService
{
    // 说明：以下 AppUser 体系为历史遗留，实体未映射到当前 DbContext（调用会 500），
    // 用户管理统一走 ABP Identity / TenantUser，故从远程 API 隐藏，避免暴露坏接口。
    [RemoteService(false)]
    Task<UserDto> GetAsync(Guid id);

    [RemoteService(false)]
    Task<PagedResultDto<UserDto>> GetListAsync(GetUserListDto input);

    [RemoteService(false)]
    Task<UserDto> CreateAsync(CreateUserDto input);

    [RemoteService(false)]
    Task UpdateAsync(Guid id, UpdateUserDto input);

    [RemoteService(false)]
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
