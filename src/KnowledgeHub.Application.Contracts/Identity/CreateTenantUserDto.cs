using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Application.Identity;

public class CreateTenantUserDto
{
    public Guid? TenantId { get; set; }

    [Required]
    public string UserName { get; set; }

    [Required]
    public string EmailAddress { get; set; }

    [Required]
    public string Password { get; set; }

    public string Name { get; set; }

    public string Surname { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Guid? MajorId { get; set; }

    public List<string> RoleNames { get; set; } = new();
}
