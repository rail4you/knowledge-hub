using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace KnowledgeHub.Users;

/// <summary>
/// 用户管理服务。
///
/// 关键修复：原先类级 [Authorize(KnowledgeHubPermissions.Users.Default)] 会隐式作用到
/// 未加 [AbpAllowAnonymous] 的所有方法上；而微软的 [AllowAnonymous] 在 ABP 10
/// dynamic API 生成 controller 时偶发未复制，导致学生调用 UpdateMyProfileAsync
/// 被 403（GetMyProfileAsync 被复制上去了所以能走）。
///
/// 以拆解类级 attribute 到 method 级的方式避免这个隐性覆盖踩坑：
/// - 管理类操作（Get/Create/Update/Delete/User）显式加 [Authorize(...)]；
/// - 学生自服务（Get/UpdateMyProfile）显式 [AllowAnonymous]，不再依赖隐式 override。
/// </summary>
public class UserAppService : KnowledgeHubAppService, IUserAppService
{
    private readonly IUserRepository _userRepository;
    private readonly UserManager _userManager;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityUserManager _identityUserManager;

    public UserAppService(
        IUserRepository userRepository,
        UserManager userManager,
        IIdentityUserRepository identityUserRepository,
        IdentityUserManager identityUserManager)
    {
        _userRepository = userRepository;
        _userManager = userManager;
        _identityUserRepository = identityUserRepository;
        _identityUserManager = identityUserManager;
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Default)]
    public async Task<UserDto> GetAsync(Guid id)
    {
        var user = await _userRepository.GetAsync(id);
        return ObjectMapper.Map<AppUser, UserDto>(user);
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Default)]
    public async Task<PagedResultDto<UserDto>> GetListAsync(GetUserListDto input)
    {
        if (input.Sorting.IsNullOrWhiteSpace())
        {
            input.Sorting = nameof(AppUser.Name);
        }

        var users = await _userRepository.GetListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting,
            input.Filter
        );

        var totalCount = input.Filter == null
            ? await _userRepository.CountAsync()
            : await _userRepository.CountAsync(
                user => user.Name.Contains(input.Filter));

        return new PagedResultDto<UserDto>(
            totalCount,
            ObjectMapper.Map<List<AppUser>, List<UserDto>>(users)
        );
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Create)]
    public async Task<UserDto> CreateAsync(CreateUserDto input)
    {
        var user = await _userManager.CreateAsync(
            input.Name,
            input.BirthDate,
            input.ShortBio
        );

        await _userRepository.InsertAsync(user);

        return ObjectMapper.Map<AppUser, UserDto>(user);
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Edit)]
    public async Task UpdateAsync(Guid id, UpdateUserDto input)
    {
        var user = await _userRepository.GetAsync(id);

        if (user.Name != input.Name)
        {
            await _userManager.ChangeNameAsync(user, input.Name);
        }

        user.BirthDate = input.BirthDate;
        user.ShortBio = input.ShortBio;

        await _userRepository.UpdateAsync(user);
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _userRepository.DeleteAsync(id);
    }

    /// <summary>
    /// 获取当前登录用户的个人资料（供学生端修改联系方式）
    /// </summary>
    [AllowAnonymous] // 显式覆盖类级 [Authorize]，确保学生未分配 Users.Default 权限也能调
    public async Task<MyProfileDto> GetMyProfileAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new UserFriendlyException("请先登录。");
        }
        // 联系方式字段存储在 ABP IdentityUser（不在项目 AppUser 实体里）
        var identity = await _identityUserRepository.GetAsync(CurrentUser.Id.Value);
        return new MyProfileDto
        {
            Id = identity.Id,
            UserName = identity.UserName ?? string.Empty,
            Name = identity.Name ?? identity.Surname ?? string.Empty,
            Email = identity.Email,
            PhoneNumber = identity.PhoneNumber,
            EmailConfirmed = identity.EmailConfirmed,
            PhoneNumberConfirmed = identity.PhoneNumberConfirmed,
        };
    }

    /// <summary>
    /// 当前登录用户更新联系方式（仅允许改 Email/Phone）
    /// </summary>
    [AllowAnonymous]
    public async Task UpdateMyProfileAsync(UpdateMyProfileDto input)
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new UserFriendlyException("请先登录。");
        }

        var identity = await _identityUserRepository.GetAsync(CurrentUser.Id.Value);

        // 仅在值变化时更新
        if (!string.IsNullOrWhiteSpace(input.PhoneNumber) && input.PhoneNumber != identity.PhoneNumber)
        {
            identity.SetPhoneNumber(input.PhoneNumber, false);
        }
        if (!string.IsNullOrWhiteSpace(input.Email) && input.Email != identity.Email)
        {
            // Email setter 不可访问，走 IdentityUserManager
            var result = await _identityUserManager.SetEmailAsync(identity, input.Email);
            if (!result.Succeeded)
            {
                throw new UserFriendlyException("邮箱更新失败：" + string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            identity.SetEmailConfirmed(false); // 邮箱变更后需重新验证
            await _identityUserRepository.UpdateAsync(identity);
        }
    }
}
