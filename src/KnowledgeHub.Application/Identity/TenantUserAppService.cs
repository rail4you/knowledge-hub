using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Identity;

[Authorize(KnowledgeHubPermissions.Users.Default)]
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

    [Authorize(KnowledgeHubPermissions.Users.Create)]
    public async Task<IdentityUserDto> CreateUserForTenantAsync(CreateTenantUserDto input)
    {
        // Non-host users can only create users in their own tenant
        if (_currentTenant.Id.HasValue)
        {
            input.TenantId = _currentTenant.Id.Value;
        }

        using (_currentTenant.Change(input.TenantId))
        {
            var user = new Volo.Abp.Identity.IdentityUser(
                GuidGenerator.Create(),
                input.UserName,
                input.EmailAddress,
                tenantId: input.TenantId
            );

            user.Name = input.Name ?? string.Empty;
            user.Surname = input.Surname ?? "-";
            user.SetIsActive(input.IsActive);
            
            var password = input.Password;
            (await _userManager.CreateAsync(user, password))
                .CheckErrors();

            if (input.RoleNames != null && input.RoleNames.Count > 0)
            {
                (await _userManager.SetRolesAsync(user, input.RoleNames))
                    .CheckErrors();
            }

            return ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, IdentityUserDto>(user);
        }
    }

    public async Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetTenantUsersInput input)
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

            // Filter by tenant - ensure tenant isolation
            if (_currentTenant.Id.HasValue)
            {
                // Non-host users: always restrict to own tenant, ignore input.TenantId
                queryable = queryable.Where(u => u.TenantId == _currentTenant.Id.Value);
            }
            else if (input.TenantId.HasValue)
            {
                // Host users: filter by specified tenant
                queryable = queryable.Where(u => u.TenantId == input.TenantId.Value);
            }
            // Host users with no TenantId filter see all users

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

            CheckTenantOwnership(user);
            return ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, IdentityUserDto>(user);
        }
    }

    public async Task<List<string>> GetRolesForUserAsync(Guid userId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(userId);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {userId}");
            }

            CheckTenantOwnership(user);

            using (_currentTenant.Change(user.TenantId))
            {
                return (await _userManager.GetRolesAsync(user)).ToList();
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Users.Edit)]
    public async Task<IdentityUserDto> UpdateAsync(Guid id, UpdateTenantUserDto input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            CheckTenantOwnership(user);

            using (_currentTenant.Change(user.TenantId))
            {
                (await _userManager.SetUserNameAsync(user, input.UserName))
                    .CheckErrors();
                (await _userManager.SetEmailAsync(user, input.Email))
                    .CheckErrors();
                user.Name = input.Name;
                user.Surname = input.Surname;
                user.SetIsActive(input.IsActive);
                user.SetPhoneNumber(input.PhoneNumber, input.PhoneNumberConfirmed);

                (await _userManager.UpdateAsync(user))
                    .CheckErrors();

                if (!string.IsNullOrWhiteSpace(input.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    (await _userManager.ResetPasswordAsync(user, token, input.Password))
                        .CheckErrors();
                }

                if (input.RoleNames != null)
                {
                    (await _userManager.SetRolesAsync(user, input.RoleNames))
                        .CheckErrors();
                }

                return ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, IdentityUserDto>(user);
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Users.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            CheckTenantOwnership(user);

            using (_currentTenant.Change(user.TenantId))
            {
                (await _userManager.DeleteAsync(user))
                    .CheckErrors();
            }
        }
    }

    /// <summary>
    /// Verify that the target user belongs to the same tenant as the current user.
    /// Host users can access any tenant's users.
    /// </summary>
    private void CheckTenantOwnership(Volo.Abp.Identity.IdentityUser user)
    {
        if (_currentTenant.Id.HasValue && user.TenantId != _currentTenant.Id)
        {
            throw new UserFriendlyException("您没有权限访问该租户的用户数据");
        }
    }
}
