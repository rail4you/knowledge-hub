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

    [Required]
    public string Name { get; set; }

    public string Surname { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Guid? MajorId { get; set; }

    public string? Major { get; set; }

    public string? ClassName { get; set; }

    public string? CompanyName { get; set; }

    public string? Course { get; set; }

    public string? Department { get; set; }

    public string? EmployeeNumber { get; set; }

    public string? Grade { get; set; }

    public string? Industry { get; set; }

    public string? ManagementScope { get; set; }

    public string? PartnerSchool { get; set; }

    public string? Position { get; set; }

    public string? Remark { get; set; }

    public string? SchoolId { get; set; }

    public string? StudentNumber { get; set; }

    public string? Title { get; set; }

    public string? UnifiedSocialCreditCode { get; set; }

    public List<string> RoleNames { get; set; } = new();
}