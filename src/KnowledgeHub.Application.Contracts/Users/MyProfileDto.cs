using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Users;

/// <summary>
/// 当前登录用户的个人资料（含联系方式）
/// </summary>
public class MyProfileDto : EntityDto<Guid>
{
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
}

/// <summary>
/// 学生本人更新联系方式（仅允许改 Email/Phone，避免误改姓名/生日）
/// </summary>
public class UpdateMyProfileDto
{
    [StringLength(256, MinimumLength = 0)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(32, MinimumLength = 0)]
    [Phone]
    public string? PhoneNumber { get; set; }
}
