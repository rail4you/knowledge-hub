using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.PermissionManagement;

namespace KnowledgeHub.HttpApi.Controllers;

[Area("app")]
[RemoteService(Name = "KnowledgeHub")]
[Route("api/knowledge-hub/permissions")]
public class TenantPermissionController : AbpController
{
    private readonly ITenantPermissionService _tenantPermissionService;

    public TenantPermissionController(ITenantPermissionService tenantPermissionService)
    {
        _tenantPermissionService = tenantPermissionService;
    }

    [HttpPost("set-for-tenant")]
    public async Task SetForTenantAsync([FromBody] SetPermissionForTenantInput input)
    {
        foreach (var permission in input.Permissions)
        {
            await _tenantPermissionService.SetForTenantAsync(
                input.TenantId,
                permission.Name,
                permission.ProviderName,
                permission.ProviderKey,
                permission.IsGranted
            );
        }
    }
}

public class SetPermissionForTenantInput
{
    public Guid? TenantId { get; set; }
    public List<PermissionItem> Permissions { get; set; } = new();
}

public class PermissionItem
{
    public string Name { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}

public class PermissionGrantInfoDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}
