using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Application.Identity;

public class UpdateTenantUserDto
{
    public Guid? TenantId { get; set; }

    [Required]
    public string UserName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    public string Name { get; set; }

    public string Surname { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> RoleNames { get; set; } = new();
}
