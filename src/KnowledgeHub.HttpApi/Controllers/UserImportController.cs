using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

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
    public async Task<UserImportResultDto> ImportAsync([FromBody] byte[] excelFile)
    {
        return await _userImportAppService.ImportAsync(excelFile);
    }
}
