using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Accounts;

/// <summary>
/// 账号有效期管理（多校协同 - 账号生命周期管控）。
/// 仅限「全局管理员（host）」使用，为租户管理员 / 教师账号配置有效期限、
/// 到期自动熔断编辑/管理权限、续期恢复权限。
/// </summary>
public interface IAccountValidityAppService : IApplicationService
{
    /// <summary>分页查询受管控角色（租户管理员/教师）的账号及有效期信息。</summary>
    Task<PagedResultDto<AccountValidityDto>> GetListAsync(GetAccountValidityListInput input);

    /// <summary>查询单个账号的有效期信息（无配置时返回永久有效）。</summary>
    Task<AccountValidityDto> GetAsync(Guid id);

    /// <summary>为单个账号设置有效期（续期 / 新设 / 永久有效）。</summary>
    Task<AccountValidityDto> SetAsync(SetAccountValidityInput input);

    /// <summary>为多个账号批量设置有效期。</summary>
    Task SetBatchAsync(SetAccountValidityBatchInput input);
}
