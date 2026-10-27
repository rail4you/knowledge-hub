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

    /// <summary>
    /// 手机号认证：提交的手机号与账号绑定手机号一致即标记为已认证。
    /// </summary>
    Task<MyProfileDto> VerifyPhoneAsync(VerifyPhoneInput input);

    /// <summary>
    /// 实名认证：提交真实姓名 + 身份证号，校验通过后标记为已实名。
    /// 已实名用户可学习对外开放自学的共享资源库 / 共建资源库课程。
    /// </summary>
    Task<MyProfileDto> VerifyRealNameAsync(VerifyRealNameInput input);
}
