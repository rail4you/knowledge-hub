using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Users;

/// <summary>
/// 当前登录用户的个人资料（含联系方式与认证状态）
/// </summary>
public class MyProfileDto : EntityDto<Guid>
{
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }

    // ── 手机认证 ────────────────────────────────────────────
    /// <summary>手机号是否已认证（= PhoneNumberConfirmed）。</summary>
    public bool IsPhoneVerified { get; set; }

    // ── 实名认证 ────────────────────────────────────────────
    /// <summary>实名（真实姓名）。</summary>
    public string? RealName { get; set; }

    /// <summary>身份证号（脱敏后返回，不返回原文）。</summary>
    public string? IdCardMasked { get; set; }

    /// <summary>是否已完成实名认证。</summary>
    public bool IsRealNameVerified { get; set; }

    /// <summary>实名认证时间。</summary>
    public DateTime? RealNameVerifiedAt { get; set; }
}

/// <summary>
/// 本人更新个人资料（姓名 / 联系方式）。
/// </summary>
/// <para>
/// 学生端只提交 Email / Phone（<c>Name</c> 为空即保持原值）；
/// 管理端「我的账户」可同时修改姓名。
/// </para>
public class UpdateMyProfileDto
{
    /// <summary>显示名 / 姓名。为空则保持原值不变。</summary>
    [StringLength(64, MinimumLength = 0)]
    public string? Name { get; set; }

    /// <summary>
    /// 电子邮箱。空串 / null = 不修改（ABP 不支持邮箱解绑，且邮箱常用作登录标识）；
    /// 非空需为合法邮箱（格式在服务端校验，不用 [EmailAddress] ——
    /// 它会把空串也判为非法，导致「清空邮箱」无法保存）。
    /// </summary>
    [StringLength(256, MinimumLength = 0)]
    public string? Email { get; set; }

    /// <summary>手机号。空串 / null = 解绑手机号；非空需为合法号码（同上，格式在服务端校验）。</summary>
    [StringLength(32, MinimumLength = 0)]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// 手机号认证。
///
/// <para>
/// 当前平台尚未接入短信网关，校验口径为「提交的手机号与账号绑定手机号一致」，
/// 一致即标记为已认证；接入短信验证码后应改为验证码校验。
/// </para>
/// </summary>
public class VerifyPhoneInput
{
    [Required]
    [StringLength(32, MinimumLength = 0)]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>
/// 实名认证（真实姓名 + 身份证号）。
/// </summary>
public class VerifyRealNameInput
{
    /// <summary>真实姓名。</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string RealName { get; set; } = string.Empty;

    /// <summary>身份证号（18 位）。</summary>
    [Required]
    [StringLength(32, MinimumLength = 15)]
    public string IdCardNumber { get; set; } = string.Empty;
}
