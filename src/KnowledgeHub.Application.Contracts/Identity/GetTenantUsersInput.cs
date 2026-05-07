using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace KnowledgeHub.Application.Identity;

public class GetTenantUsersInput : GetIdentityUsersInput
{
    public Guid? TenantId { get; set; }
}
