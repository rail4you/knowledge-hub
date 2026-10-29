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

    [Required]
    public string Name { get; set; }

    public string? Surname { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool IsActive { get; set; } = true;

    public bool TwoFactorEnabled { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public Guid? MajorId { get; set; }

    public string? ClassName { get; set; }

    /// <summary>所属教学班 Id：与教学班成员表同步，学生归班用。</summary>
    public Guid? ClassId { get; set; }

    /// <summary>
    /// 是否退出当前教学班（退班）。
    ///
    /// <para>与 ClassId 区分：不传 ClassId 且不置本标志 = 班级保持不变；
    /// 置本标志表示把学生从原班级移出（回退到「未分班」）。</para>
    /// </summary>
    public bool RemoveFromClass { get; set; }

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

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public List<string> RoleNames { get; set; } = new();
}
