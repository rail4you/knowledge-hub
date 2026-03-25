using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Application.Identity;

public class CreateTenantUserDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public string UserName { get; set; }

    [Required]
    public string EmailAddress { get; set; }

    [Required]
    public string Password { get; set; }

    public string Name { get; set; }

    public string Surname { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> RoleNames { get; set; } = new();
}
