using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Controllers;

[Route("api/public")]
[AllowAnonymous]
public class TenantListController : AbpControllerBase
{
    private readonly ITenantRepository _tenantRepository;

    public TenantListController(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    [HttpGet("tenants")]
    public async Task<List<TenantInfoDto>> GetTenants()
    {
        var tenants = await _tenantRepository.GetListAsync();
        
        var result = new List<TenantInfoDto>
        {
            new TenantInfoDto { Id = null, Name = "全局" }
        };
        
        result.AddRange(tenants
            .Select(t => new TenantInfoDto { Id = t.Id.ToString(), Name = t.Name }));
        
        return result;
    }
}

public class TenantInfoDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}
