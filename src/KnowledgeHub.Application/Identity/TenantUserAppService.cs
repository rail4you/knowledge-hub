using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Identity;

public class TenantUserAppService : KnowledgeHubAppService, ITenantUserAppService
{
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Volo.Abp.Identity.IdentityUser, Guid> _userRepository;

    public TenantUserAppService(
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> userRepository)
    {
        _userManager = userManager;
        _currentTenant = currentTenant;
        _userRepository = userRepository;
    }

    public async Task<IdentityUserDto> CreateUserForTenantAsync(CreateTenantUserDto input)
    {
        using (_currentTenant.Change(input.TenantId))
        {
            var user = new Volo.Abp.Identity.IdentityUser(
                GuidGenerator.Create(),
                input.UserName,
                input.EmailAddress,
                tenantId: input.TenantId
            );

            user.Name = input.Name;
            user.Surname = input.Surname;
            user.SetIsActive(input.IsActive);

            (await _userManager.CreateAsync(user, input.Password))
                .CheckErrors();

            if (input.RoleNames != null && input.RoleNames.Count > 0)
            {
                (await _userManager.SetRolesAsync(user, input.RoleNames))
                    .CheckErrors();
            }

            return ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, IdentityUserDto>(user);
        }
    }

    public async Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetIdentityUsersInput input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _userRepository.GetQueryableAsync();
            
            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                queryable = queryable.Where(u => 
                    u.UserName.Contains(input.Filter) || 
                    u.Email.Contains(input.Filter) ||
                    (u.Name != null && u.Name.Contains(input.Filter)) ||
                    (u.Surname != null && u.Surname.Contains(input.Filter)));
            }

            var totalCount = await queryable.CountAsync();
            
            var users = await queryable
                .OrderByDescending(u => u.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<IdentityUserDto>(
                totalCount,
                ObjectMapper.Map<System.Collections.Generic.List<Volo.Abp.Identity.IdentityUser>, System.Collections.Generic.List<IdentityUserDto>>(users)
            );
        }
    }

    public async Task<IdentityUserDto> GetAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }
            return ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, IdentityUserDto>(user);
        }
    }

    public async Task<IdentityUserDto> UpdateAsync(Guid id, UpdateTenantUserDto input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            using (_currentTenant.Change(user.TenantId))
            {
                (await _userManager.SetUserNameAsync(user, input.UserName))
                    .CheckErrors();
                (await _userManager.SetEmailAsync(user, input.Email))
                    .CheckErrors();
                user.Name = input.Name;
                user.Surname = input.Surname;
                user.SetIsActive(input.IsActive);

                (await _userManager.UpdateAsync(user))
                    .CheckErrors();

                if (input.RoleNames != null)
                {
                    (await _userManager.SetRolesAsync(user, input.RoleNames))
                        .CheckErrors();
                }

                return ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, IdentityUserDto>(user);
            }
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            using (_currentTenant.Change(user.TenantId))
            {
                (await _userManager.DeleteAsync(user))
                    .CheckErrors();
            }
        }
    }
}
