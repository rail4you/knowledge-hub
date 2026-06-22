using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Users;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Users;

public interface IUserImportAppService : IApplicationService
{
    Task<UserImportResultDto> ImportAsync(byte[] excelFile);

    /// <summary>
    /// P1-2：返回系统预置的「角色 → 中文名 → 已授予权限数」清单，便于管理员核对 SchoolAdmin vs LeagueAdmin 等角色的实际差异
    /// </summary>
    Task<List<RolePermissionSummaryDto>> GetRolePermissionSummaryAsync();
}
