using System;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.PublicRegistrations.Dtos;

/// <summary>
/// 公开注册请求（社会学校）。
/// </summary>
public class PublicRegistrationDto
{
    [Required]
    [StringLength(64)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [StringLength(64)]
    public string? Name { get; set; }

    [StringLength(64)]
    public string? Surname { get; set; }

    [Phone]
    [StringLength(32)]
    public string? PhoneNumber { get; set; }
}

/// <summary>公开注册结果。</summary>
public class PublicRegistrationResultDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    /// <summary>注册用户归属的社会学校名称。</summary>
    public string SocialSchoolName { get; set; } = string.Empty;

    /// <summary>注册用户归属的社会学校租户 Id。</summary>
    public Guid SocialSchoolTenantId { get; set; }
}
