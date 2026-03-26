using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.Identity;

public class IdentityUserWithRolesDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string UserName { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsActive { get; set; }
    public List<string> RoleNames { get; set; } = new();
}
