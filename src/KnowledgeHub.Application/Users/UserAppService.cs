using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Users;

[Authorize(KnowledgeHubPermissions.Users.Default)]
public class UserAppService : KnowledgeHubAppService, IUserAppService
{
    private readonly IUserRepository _userRepository;
    private readonly UserManager _userManager;

    public UserAppService(
        IUserRepository userRepository,
        UserManager userManager)
    {
        _userRepository = userRepository;
        _userManager = userManager;
    }

    public async Task<UserDto> GetAsync(Guid id)
    {
        var user = await _userRepository.GetAsync(id);
        return ObjectMapper.Map<AppUser, UserDto>(user);
    }

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

    [Authorize(KnowledgeHubPermissions.Users.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _userRepository.DeleteAsync(id);
    }
}
