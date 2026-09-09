using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Identity;

public interface ITenantRoleAppService : IApplicationService
{
    Task<PagedResultDto<TenantRoleDto>> GetListAsync(GetTenantRolesInput input);
    
    Task<TenantRoleDto> GetAsync(Guid id);
    
    Task<TenantRoleDto> CreateAsync(CreateTenantRoleDto input);
    
    Task<TenantRoleDto> UpdateAsync(Guid id, UpdateTenantRoleDto input);
    
    Task DeleteAsync(Guid id);
}

public class TenantRoleDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsStatic { get; set; }
    public bool IsPublic { get; set; }
    public Guid? TenantId { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

public class GetTenantRolesInput : PagedAndSortedResultRequestDto
{
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 仅查询全局（TenantId == null）角色。host 管理员选择“全局”时使用。
    /// 为 true 时优先于 <see cref="TenantId"/>。
    /// </summary>
    public bool? OnlyHost { get; set; }
    public string? Filter { get; set; }
}

public class CreateTenantRoleDto
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsPublic { get; set; } = true;
}

public class UpdateTenantRoleDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? ConcurrencyStamp { get; set; }
}
