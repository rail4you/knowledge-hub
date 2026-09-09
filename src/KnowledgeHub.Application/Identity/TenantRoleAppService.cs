using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Identity;

[Authorize]
public class TenantRoleAppService : KnowledgeHubAppService, ITenantRoleAppService
{
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<IdentityRole, Guid> _repository;

    public TenantRoleAppService(
        IIdentityRoleRepository roleRepository,
        ICurrentTenant currentTenant,
        IRepository<IdentityRole, Guid> repository)
    {
        _roleRepository = roleRepository;
        _currentTenant = currentTenant;
        _repository = repository;
    }

    public async Task<PagedResultDto<TenantRoleDto>> GetListAsync(GetTenantRolesInput input)
    {
        // 租户上下文（如 qidi-admin SchoolAdmin）：仅能查看本租户的角色。
        // 不能跨租户查看，也不能“查看全部”或其他租户的角色。
        if (CurrentTenant.Id.HasValue)
        {
            if (input.TenantId.HasValue && input.TenantId.Value != CurrentTenant.Id.Value)
            {
                throw new AbpAuthorizationException("仅全局管理员可查看其他租户的角色。");
            }
            input.TenantId = CurrentTenant.Id.Value;
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _repository.GetQueryableAsync();

            // 联盟管理员是全局角色（TenantId=null），租户内不应存在：
            // 列表中隐藏租户级 LeagueAdmin 副本，仅保留全局的。
            queryable = queryable.Where(r => r.TenantId == null || r.Name != "LeagueAdmin");

            if (input.OnlyHost == true)
            {
                queryable = queryable.Where(r => r.TenantId == null);
            }
            else if (input.TenantId.HasValue)
            {
                queryable = queryable.Where(r => r.TenantId == input.TenantId);
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                queryable = queryable.Where(r => r.Name.Contains(input.Filter));
            }

            var totalCount = await queryable.CountAsync();

            var roles = await queryable
                .OrderBy(r => r.TenantId == null ? 0 : 1)
                .ThenBy(r => r.Name)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            var dtos = roles.Select(r => new TenantRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                IsDefault = r.IsDefault,
                IsStatic = r.IsStatic,
                IsPublic = r.IsPublic,
                TenantId = r.TenantId,
                ConcurrencyStamp = r.ConcurrencyStamp
            }).ToList();

            return new PagedResultDto<TenantRoleDto>(totalCount, dtos);
        }
    }

    public async Task<TenantRoleDto> GetAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var role = await _repository.FindAsync(id);
            if (role == null)
            {
                throw new UserFriendlyException($"角色不存在: {id}");
            }

            // 租户上下文：拒绝跨租户访问角色。
            EnsureRoleInCurrentTenant(role);

            return new TenantRoleDto
            {
                Id = role.Id,
                Name = role.Name,
                IsDefault = role.IsDefault,
                IsStatic = role.IsStatic,
                IsPublic = role.IsPublic,
                TenantId = role.TenantId,
                ConcurrencyStamp = role.ConcurrencyStamp
            };
        }
    }

    [Authorize("AbpIdentity.Roles.ManagePermissions")]
    public async Task<TenantRoleDto> CreateAsync(CreateTenantRoleDto input)
    {
        // 租户上下文：强制创建到本租户，忽略请求中传入的其他 TenantId。
        if (CurrentTenant.Id.HasValue)
        {
            if (input.TenantId.HasValue && input.TenantId.Value != CurrentTenant.Id.Value)
            {
                throw new AbpAuthorizationException("仅全局管理员可在其他租户下创建角色。");
            }
            input.TenantId = CurrentTenant.Id.Value;
        }

        // 联盟管理员是全局角色，禁止在租户内创建同名角色。
        if (input.TenantId.HasValue
            && string.Equals(input.Name, "LeagueAdmin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("租户内不允许创建“联盟管理员”角色，联盟管理员为全局角色。");
        }

        using (_currentTenant.Change(input.TenantId))
        {
            var role = new IdentityRole(
                GuidGenerator.Create(),
                input.Name,
                input.TenantId
            )
            {
                IsDefault = input.IsDefault,
                IsPublic = input.IsPublic
            };

            await _roleRepository.InsertAsync(role);

            return new TenantRoleDto
            {
                Id = role.Id,
                Name = role.Name,
                IsDefault = role.IsDefault,
                IsStatic = role.IsStatic,
                IsPublic = role.IsPublic,
                TenantId = role.TenantId,
                ConcurrencyStamp = role.ConcurrencyStamp
            };
        }
    }

    [Authorize("AbpIdentity.Roles.ManagePermissions")]
    public async Task<TenantRoleDto> UpdateAsync(Guid id, UpdateTenantRoleDto input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var role = await _repository.FindAsync(id);
            if (role == null)
            {
                throw new UserFriendlyException($"角色不存在: {id}");
            }

            // 租户上下文：仅允许修改本租户的角色。
            EnsureRoleInCurrentTenant(role);

            using (_currentTenant.Change(role.TenantId))
            {
                role.ChangeName(input.Name);
                role.IsDefault = input.IsDefault;
                role.IsPublic = input.IsPublic;

                await _roleRepository.UpdateAsync(role);

                return new TenantRoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    IsDefault = role.IsDefault,
                    IsStatic = role.IsStatic,
                    IsPublic = role.IsPublic,
                    TenantId = role.TenantId,
                    ConcurrencyStamp = role.ConcurrencyStamp
                };
            }
        }
    }

    [Authorize("AbpIdentity.Roles.ManagePermissions")]
    public async Task DeleteAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var role = await _repository.FindAsync(id);
            if (role == null)
            {
                throw new UserFriendlyException($"角色不存在: {id}");
            }

            // 租户上下文：仅允许删除本租户的角色。
            EnsureRoleInCurrentTenant(role);

            using (_currentTenant.Change(role.TenantId))
            {
                await _roleRepository.DeleteAsync(role);
            }
        }
    }

    /// <summary>
    /// 租户上下文（如 qidi-admin）下，仅允许操作本租户的角色。
    /// host 全局管理员不受此限制。
    /// </summary>
    private void EnsureRoleInCurrentTenant(IdentityRole role)
    {
        if (CurrentTenant.Id.HasValue && role.TenantId != CurrentTenant.Id.Value)
        {
            throw new AbpAuthorizationException("仅全局管理员可操作其他租户的角色。");
        }
    }
}
