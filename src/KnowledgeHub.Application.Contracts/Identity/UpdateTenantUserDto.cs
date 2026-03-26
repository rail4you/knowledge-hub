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

    public bool EmailConfirmed { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool IsActive { get; set; } = true;

    public bool TwoFactorEnabled { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public string? ClassName { get; set; }

    public string? CompanyName { get; set; }

    public string? Course { get; set; }

    public string? Department { get; set; }

    public string? EmployeeNumber { get; set; }

    public string? Grade { get; set; }

    public string? Industry { get; set; }

    public string? Major { get; set; }

    public string? ManagementScope { get; set; }

    public string? PartnerSchool { get; set; }

    public string? Position { get; set; }

    public string? Remark { get; set; }

    public int RoleType { get; set; }

    public string? SchoolId { get; set; }

    public string? StudentNumber { get; set; }

    public string? Title { get; set; }

    public string? UnifiedSocialCreditCode { get; set; }

    public List<string> RoleNames { get; set; } = new();
}
