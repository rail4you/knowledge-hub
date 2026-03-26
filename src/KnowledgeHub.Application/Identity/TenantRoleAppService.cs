using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Identity;

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
        using (DataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _repository.GetQueryableAsync();

            if (input.TenantId.HasValue)
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

    public async Task<TenantRoleDto> CreateAsync(CreateTenantRoleDto input)
    {
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

    public async Task<TenantRoleDto> UpdateAsync(Guid id, UpdateTenantRoleDto input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var role = await _repository.FindAsync(id);
            if (role == null)
            {
                throw new UserFriendlyException($"角色不存在: {id}");
            }

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

    public async Task DeleteAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var role = await _repository.FindAsync(id);
            if (role == null)
            {
                throw new UserFriendlyException($"角色不存在: {id}");
            }

            using (_currentTenant.Change(role.TenantId))
            {
                await _roleRepository.DeleteAsync(role);
            }
        }
    }
}
