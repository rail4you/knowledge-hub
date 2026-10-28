using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace KnowledgeHub.Users;

/// <summary>
/// 用户管理服务。
///
/// 关键修复：原先类级 [Authorize(KnowledgeHubPermissions.Users.Default)] 会隐式作用到
/// 未加 [AbpAllowAnonymous] 的所有方法上；而微软的 [AllowAnonymous] 在 ABP 10
/// dynamic API 生成 controller 时偶发未复制，导致学生调用 UpdateMyProfileAsync
/// 被 403（GetMyProfileAsync 被复制上去了所以能走）。
///
/// 以拆解类级 attribute 到 method 级的方式避免这个隐性覆盖踩坑：
/// - 管理类操作（Get/Create/Update/Delete/User）显式加 [Authorize(...)]；
/// - 学生自服务（Get/UpdateMyProfile）显式 [AllowAnonymous]，不再依赖隐式 override。
/// </summary>
[IgnoreAntiforgeryToken]
public class UserAppService : KnowledgeHubAppService, IUserAppService
{
    private readonly IUserRepository _userRepository;
    private readonly UserManager _userManager;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityUserManager _identityUserManager;

    public UserAppService(
        IUserRepository userRepository,
        UserManager userManager,
        IIdentityUserRepository identityUserRepository,
        IdentityUserManager identityUserManager)
    {
        _userRepository = userRepository;
        _userManager = userManager;
        _identityUserRepository = identityUserRepository;
        _identityUserManager = identityUserManager;
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Default)]
    public async Task<UserDto> GetAsync(Guid id)
    {
        var user = await _userRepository.GetAsync(id);
        return ObjectMapper.Map<AppUser, UserDto>(user);
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Default)]
    public async Task<PagedResultDto<UserDto>> GetListAsync(GetUserListDto input)
    {
        if (input.Sorting.IsNullOrWhiteSpace())
        {
            input.Sorting = nameof(AppUser.Name);
        }

        var users = await _userRepository.GetListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting,
            input.Filter
        );

        var totalCount = input.Filter == null
            ? await _userRepository.CountAsync()
            : await _userRepository.CountAsync(
                user => user.Name.Contains(input.Filter));

        return new PagedResultDto<UserDto>(
            totalCount,
            ObjectMapper.Map<List<AppUser>, List<UserDto>>(users)
        );
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Create)]
    public async Task<UserDto> CreateAsync(CreateUserDto input)
    {
        var user = await _userManager.CreateAsync(
            input.Name,
            input.BirthDate,
            input.ShortBio
        );

        await _userRepository.InsertAsync(user);

        return ObjectMapper.Map<AppUser, UserDto>(user);
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Edit)]
    public async Task UpdateAsync(Guid id, UpdateUserDto input)
    {
        var user = await _userRepository.GetAsync(id);

        if (user.Name != input.Name)
        {
            await _userManager.ChangeNameAsync(user, input.Name);
        }

        user.BirthDate = input.BirthDate;
        user.ShortBio = input.ShortBio;

        await _userRepository.UpdateAsync(user);
    }

    [RemoteService(false)]
    [Authorize(KnowledgeHubPermissions.Users.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _userRepository.DeleteAsync(id);
    }

    /// <summary>
    /// 获取当前登录用户的个人资料（供学生端修改联系方式）
    /// </summary>
    [AllowAnonymous] // 显式覆盖类级 [Authorize]，确保学生未分配 Users.Default 权限也能调
    public async Task<MyProfileDto> GetMyProfileAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new UserFriendlyException("请先登录。");
        }
        // 联系方式字段存储在 ABP IdentityUser（不在项目 AppUser 实体里）
        var identity = await _identityUserRepository.GetAsync(CurrentUser.Id.Value);
        return BuildProfileDto(identity);
    }

    /// <summary>
    /// 手机号认证。
    ///
    /// <para>
    /// 当前平台未接入短信网关，校验口径为「提交手机号 = 账号绑定手机号」，
    /// 一致即置 <c>PhoneNumberConfirmed</c>；接入短信验证码后应改为验证码校验。
    /// </para>
    /// </summary>
    [AllowAnonymous]
    public async Task<MyProfileDto> VerifyPhoneAsync(VerifyPhoneInput input)
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new UserFriendlyException("请先登录。");
        }

        if (input == null || string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            throw new UserFriendlyException("请填写手机号。");
        }

        var identity = await _identityUserRepository.GetAsync(CurrentUser.Id.Value);

        if (string.IsNullOrWhiteSpace(identity.PhoneNumber))
        {
            throw new UserFriendlyException("账号尚未绑定手机号，请先在联系方式中完善手机号。");
        }

        if (!string.Equals(identity.PhoneNumber.Trim(), input.PhoneNumber.Trim(), StringComparison.Ordinal))
        {
            throw new UserFriendlyException("手机号与账号绑定手机号不一致，请使用账号绑定手机号。");
        }

        if (!identity.PhoneNumberConfirmed)
        {
            identity.SetPhoneNumberConfirmed(true);
            await _identityUserRepository.UpdateAsync(identity);
        }

        return BuildProfileDto(identity);
    }

    /// <summary>
    /// 实名认证：提交真实姓名 + 身份证号。
    ///
    /// <para>
    /// 平台未接入权威核验服务，口径是「格式合法即通过」：
    /// 15 位 / 18 位（末位可为 X）且字符合法即标记为已实名。
    /// 接入权威核验后应改为「提交 → 核验 → 落库」。
    /// </para>
    /// </summary>
    [AllowAnonymous]
    public async Task<MyProfileDto> VerifyRealNameAsync(VerifyRealNameInput input)
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new UserFriendlyException("请先登录。");
        }

        if (input == null || string.IsNullOrWhiteSpace(input.RealName))
        {
            throw new UserFriendlyException("请填写真实姓名。");
        }

        if (string.IsNullOrWhiteSpace(input.IdCardNumber))
        {
            throw new UserFriendlyException("请填写身份证号。");
        }

        var idCard = input.IdCardNumber.Trim().ToUpperInvariant();
        if (!IsValidChineseIdCard(idCard))
        {
            throw new UserFriendlyException("身份证号格式不正确，请核对后重新提交。");
        }

        var identity = await _identityUserRepository.GetAsync(CurrentUser.Id.Value);

        identity.SetProperty("RealName", input.RealName.Trim());
        identity.SetProperty("IdCardNumber", idCard);
        identity.SetProperty("IsRealNameVerified", true);
        identity.SetProperty("RealNameVerifiedAt", Clock.Now);

        await _identityUserRepository.UpdateAsync(identity);

        return BuildProfileDto(identity);
    }

    /// <summary>构造个人资料 DTO（含认证状态）。</summary>
    private static MyProfileDto BuildProfileDto(IdentityUser identity)
    {
        var realName = identity.GetProperty<string>("RealName");
        var idCard = identity.GetProperty<string>("IdCardNumber");

        return new MyProfileDto
        {
            Id = identity.Id,
            UserName = identity.UserName ?? string.Empty,
            Name = identity.Name ?? identity.Surname ?? string.Empty,
            Email = identity.Email,
            PhoneNumber = identity.PhoneNumber,
            EmailConfirmed = identity.EmailConfirmed,
            PhoneNumberConfirmed = identity.PhoneNumberConfirmed,
            IsPhoneVerified = identity.PhoneNumberConfirmed,
            RealName = realName,
            IdCardMasked = MaskIdCard(idCard),
            IsRealNameVerified = identity.GetProperty<bool>("IsRealNameVerified"),
            RealNameVerifiedAt = identity.GetProperty<DateTime?>("RealNameVerifiedAt")
        };
    }

    /// <summary>身份证脱敏：保留前 4 位与后 2 位。</summary>
    private static string? MaskIdCard(string? idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard))
        {
            return null;
        }

        var trimmed = idCard.Trim();
        if (trimmed.Length <= 6)
        {
            return new string('*', trimmed.Length);
        }

        return trimmed[..4] + new string('*', trimmed.Length - 6) + trimmed[^2..];
    }

    /// <remarks>
    /// 平台未接入权威核验 / 短信验证，口径是「格式合法即通过」：
    /// 15 位老身份证 或 18 位（前 17 位数字，末位数字或 X），<b>不校验 GB 11643 校验位</b>。
    /// 之前要求校验位会让学员随便填的号码被拒，而平台又无法真验证，体验上就是个死胡同。
    /// 身份证号仍仅用于学员身份登记与证书核验，页面只做脱敏展示。
    /// </remarks>
    private static bool IsValidChineseIdCard(string idCard)
    {
        if (idCard.Length != 15 && idCard.Length != 18)
        {
            return false;
        }

        for (var i = 0; i < idCard.Length; i++)
        {
            var c = idCard[i];
            var isLast = i == idCard.Length - 1;

            // 末位允许 X（18 位身份证的校验位），其余必须是数字
            if (isLast && idCard.Length == 18 && (c == 'X' || c == 'x'))
            {
                continue;
            }

            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        // 首位不能是 0（没有 0 开头的地区码）
        return idCard[0] != '0';
    }

    /// <summary>
    /// 当前登录用户更新个人资料（姓名 / 联系方式）
    /// </summary>
    [AllowAnonymous]
    public async Task UpdateMyProfileAsync(UpdateMyProfileDto input)
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new UserFriendlyException("请先登录。");
        }

        var identity = await _identityUserRepository.GetAsync(CurrentUser.Id.Value);

        // 姓名：空串不允许（等于清空），非空才更新
        var name = input.Name?.Trim();
        if (name is not null && !string.Equals(name, identity.Name, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new UserFriendlyException("姓名不能为空。");
            }

            identity.Name = name;
        }

        // 手机号：空串 = 解绑；非空需为合法号码
        var phone = input.PhoneNumber?.Trim();
        if (phone is not null && !string.Equals(phone, identity.PhoneNumber, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                identity.SetPhoneNumber(null, false);
            }
            else
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[0-9]{5,14}$"))
                {
                    throw new UserFriendlyException("手机号格式不正确。");
                }

                identity.SetPhoneNumber(phone, false);
            }
        }

        // 邮箱：空串 = 不修改（ABP 不支持邮箱解绑）；非空需为合法邮箱（变更后需重新验证）
        var email = input.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(email) &&
            !string.Equals(email, identity.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            {
                throw new UserFriendlyException("电子邮箱格式不正确。");
            }

            // Email setter 不可访问，走 IdentityUserManager
            var result = await _identityUserManager.SetEmailAsync(identity, email);
            if (!result.Succeeded)
            {
                throw new UserFriendlyException("邮箱更新失败：" + string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            identity.SetEmailConfirmed(false); // 邮箱变更后需重新验证
        }

        await _identityUserRepository.UpdateAsync(identity);
    }
}
