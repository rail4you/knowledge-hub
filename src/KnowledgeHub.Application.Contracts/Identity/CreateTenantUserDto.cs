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

    /// <summary>所属教学班 Id：与教学班成员表同步，学生归班用。</summary>
    public Guid? ClassId { get; set; }

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

    /// <summary>
    /// 企业账号绑定的企业主体（V2.0）。传了就直接绑定；为空的 EnterpriseUser 会按
    /// <see cref="CompanyName"/> 自动建一家「草稿」企业并绑定为管理员，便于学校侧开通企业账号。
    /// </summary>
    public Guid? EnterpriseProfileId { get; set; }

    public List<string> RoleNames { get; set; } = new();
}