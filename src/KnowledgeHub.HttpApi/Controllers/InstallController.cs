using System.Threading.Tasks;
using KnowledgeHub.Install;
using KnowledgeHub.Install.Dto;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("app")]
[Route("api/app/install")]
public class InstallController : AbpControllerBase
{
    private readonly IInstallAppService _installAppService;

    public InstallController(IInstallAppService installAppService)
    {
        _installAppService = installAppService;
    }

    [HttpGet]
    [Route("status")]
    public async Task<InstallStatusDto> GetStatusAsync()
    {
        return await _installAppService.GetStatusAsync();
    }

    [HttpPost]
    [Route("install")]
    public async Task InstallAsync([FromBody] InstallInputDto input)
    {
        await _installAppService.InstallAsync(input);
    }
}