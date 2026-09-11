using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Content;

namespace KnowledgeHub.Controllers;

[Area("app")]
[Route("api/app/user-import")]
[Authorize(KnowledgeHubPermissions.Users.Import)]
public class UserImportController : AbpController, IUserImportAppService
{
    private readonly IUserImportAppService _userImportAppService;

    public UserImportController(IUserImportAppService userImportAppService)
    {
        _userImportAppService = userImportAppService;
    }

    [HttpPost]
    public async Task<UserImportResultDto> ImportAsync([FromBody] ImportUsersFileDto input)
    {
        return await _userImportAppService.ImportAsync(input);
    }

    // 注意：这两个接口同时由 ABP conventional controller 按同名路由自动暴露
    // （GET api/app/user-import/role-permission-summary、GET api/app/user-import/import-template）。
    // 此处必须标 [NonAction] 退出路由，否则请求时会报 AmbiguousMatchException（500）。
    [NonAction]
    public async Task<List<RolePermissionSummaryDto>> GetRolePermissionSummaryAsync()
    {
        return await _userImportAppService.GetRolePermissionSummaryAsync();
    }

    [NonAction]
    public async Task<IRemoteStreamContent> GetImportTemplateAsync()
    {
        return await _userImportAppService.GetImportTemplateAsync();
    }
}
