using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace KnowledgeHub.Application.Identity;

public class TenantUserDto : IdentityUserDto
{
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
}
