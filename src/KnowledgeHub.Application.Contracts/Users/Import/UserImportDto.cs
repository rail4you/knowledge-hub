using System;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.Users;

namespace KnowledgeHub.Users;

public class UserImportDto
{
    public UserRoleType RoleType { get; set; }

    [Required]
    [StringLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Email { get; set; }

    public Guid? SchoolId { get; set; }

    [StringLength(50)]
    public string? EmployeeNumber { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    [StringLength(100)]
    public string? Major { get; set; }

    [StringLength(200)]
    public string? Course { get; set; }

    [StringLength(50)]
    public string? Title { get; set; }

    [StringLength(50)]
    public string? StudentNumber { get; set; }

    [StringLength(50)]
    public string? Grade { get; set; }

    [StringLength(50)]
    public string? ClassName { get; set; }

    [StringLength(500)]
    public string? ManagementScope { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(18)]
    public string? UnifiedSocialCreditCode { get; set; }

    [StringLength(50)]
    public string? Position { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(500)]
    public string? PartnerSchool { get; set; }

    [StringLength(500)]
    public string? Remark { get; set; }
}
