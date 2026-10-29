using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Identity;

[Authorize(KnowledgeHubPermissions.Users.Default)]
[IgnoreAntiforgeryToken]
public class TenantUserAppService : KnowledgeHubAppService, ITenantUserAppService
{
    public const string MajorIdExtraProperty = "MajorId";

    /// <summary>角色名 → UserRoleType（应用只支持单用户角色，取第一个即可）。</summary>
    private static UserRoleType? GetRoleTypeFromRoleName(string? roleName)
    {
        return roleName switch
        {
            "LeagueAdmin" => UserRoleType.LeagueAdmin,
            "SchoolAdmin" => UserRoleType.SchoolAdmin,
            "Teacher" => UserRoleType.Teacher,
            "Student" => UserRoleType.Student,
            "EnterpriseUser" => UserRoleType.EnterpriseUser,
            _ => null,
        };
    }

    private sealed class ProfilePropertyValues
    {
        public string? SchoolId { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? Department { get; set; }
        public string? Course { get; set; }
        public string? Title { get; set; }
        public string? StudentNumber { get; set; }
        public string? Grade { get; set; }
        public string? ClassName { get; set; }
        public string? ManagementScope { get; set; }
        public string? CompanyName { get; set; }
        public string? UnifiedSocialCreditCode { get; set; }
        public string? Position { get; set; }
        public string? Industry { get; set; }
        public string? PartnerSchool { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>
    /// 把用户资料扩展字段写入 ExtraProperties（映射为独立列）。
    /// 非空写入、为空则移除，保证表单清空字段后能真正清除。
    /// </summary>
    private static void ApplyProfileProperties(Volo.Abp.Identity.IdentityUser user, ProfilePropertyValues value)
    {
        SetOrRemoveProperty(user, "SchoolId", value.SchoolId);
        SetOrRemoveProperty(user, "EmployeeNumber", value.EmployeeNumber);
        SetOrRemoveProperty(user, "Department", value.Department);
        SetOrRemoveProperty(user, "Course", value.Course);
        SetOrRemoveProperty(user, "Title", value.Title);
        SetOrRemoveProperty(user, "StudentNumber", value.StudentNumber);
        SetOrRemoveProperty(user, "Grade", value.Grade);
        SetOrRemoveProperty(user, "ClassName", value.ClassName);
        SetOrRemoveProperty(user, "ManagementScope", value.ManagementScope);
        SetOrRemoveProperty(user, "CompanyName", value.CompanyName);
        SetOrRemoveProperty(user, "UnifiedSocialCreditCode", value.UnifiedSocialCreditCode);
        SetOrRemoveProperty(user, "Position", value.Position);
        SetOrRemoveProperty(user, "Industry", value.Industry);
        SetOrRemoveProperty(user, "PartnerSchool", value.PartnerSchool);
        SetOrRemoveProperty(user, "Remark", value.Remark);
    }

    private static void SetOrRemoveProperty(Volo.Abp.Identity.IdentityUser user, string name, string? value)
    {
        // 注意：这些扩展属性映射为 AbpUsers 的独立列，EF 保存时只同步字典中“存在 key”的列，
        // 因此清空用 RemoveProperty 不会清掉旧值，必须显式 SetProperty(name, null)。
        user.SetProperty(name, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Volo.Abp.Identity.IdentityUser, Guid> _userRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    /// <summary>教学班（学生归班的载体）。</summary>
    private readonly IRepository<TeachingClass, Guid> _classRepository;
    /// <summary>教学班成员（「学生 ↔ 班级」关系的事实来源）。</summary>
    private readonly IRepository<TeachingClassMember, Guid> _classMemberRepository;

    public TenantUserAppService(
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> userRepository,
        IRepository<Major, Guid> majorRepository,
        IRepository<TeachingClass, Guid> classRepository,
        IRepository<TeachingClassMember, Guid> classMemberRepository)
    {
        _userManager = userManager;
        _currentTenant = currentTenant;
        _userRepository = userRepository;
        _majorRepository = majorRepository;
        _classRepository = classRepository;
        _classMemberRepository = classMemberRepository;
    }

    [Authorize(KnowledgeHubPermissions.Users.Create)]
    public async Task<TenantUserDto> CreateUserForTenantAsync(CreateTenantUserDto input)
    {
        // Non-host users can only create users in their own tenant
        if (_currentTenant.Id.HasValue)
        {
            input.TenantId = _currentTenant.Id.Value;
        }

        // 联盟管理员是全局角色（TenantId=null），租户内不应存在。
        // 目标租户非空时拒绝分配 LeagueAdmin，防止租户管理员（或 host 代建）
        // 在租户内创建联盟管理员用户。
        EnsureNoLeagueAdminForTenantUser(input.TenantId, input.RoleNames);

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("名称不能为空");
        }

        using (_currentTenant.Change(input.TenantId))
        {
            var user = new Volo.Abp.Identity.IdentityUser(
                GuidGenerator.Create(),
                input.UserName,
                input.EmailAddress,
                tenantId: input.TenantId
            );

            user.Name = input.Name.Trim();
            user.Surname = input.Surname ?? "-";
            user.SetIsActive(input.IsActive);

            if (input.MajorId.HasValue)
            {
                user.SetProperty(MajorIdExtraProperty, input.MajorId.Value);
                user.SetProperty("Major", null);
            }
            else if (!string.IsNullOrWhiteSpace(input.Major))
            {
                user.RemoveProperty(MajorIdExtraProperty);
                user.SetProperty("Major", input.Major.Trim());
            }

            // 新建时同步角色与资料扩展字段（工号 / 班级 / 院校等），与编辑、导入保持一致。
            var roleType = GetRoleTypeFromRoleName(input.RoleNames?.FirstOrDefault());
            if (roleType.HasValue)
            {
                user.SetProperty("RoleType", (int)roleType.Value);
            }

            ApplyProfileProperties(user, new ProfilePropertyValues
            {
                SchoolId = input.SchoolId,
                EmployeeNumber = input.EmployeeNumber,
                Department = input.Department,
                Course = input.Course,
                Title = input.Title,
                StudentNumber = input.StudentNumber,
                Grade = input.Grade,
                ClassName = input.ClassName,
                ManagementScope = input.ManagementScope,
                CompanyName = input.CompanyName,
                UnifiedSocialCreditCode = input.UnifiedSocialCreditCode,
                Position = input.Position,
                Industry = input.Industry,
                PartnerSchool = input.PartnerSchool,
                Remark = input.Remark,
            });

            var password = input.Password;
            (await _userManager.CreateAsync(user, password))
                .CheckErrors();

            if (input.RoleNames != null && input.RoleNames.Count > 0)
            {
                (await _userManager.SetRolesAsync(user, input.RoleNames))
                    .CheckErrors();
            }

            // 学生归班：建立「学生 ↔ 教学班」成员关系，并回写班级名文本
            if (input.ClassId.HasValue)
            {
                await SyncStudentClassAsync(user, input.ClassId);
            }

            return await MapToDtoAsync(user);
        }
    }

    public async Task<PagedResultDto<TenantUserDto>> GetListAsync(GetTenantUsersInput input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _userRepository.GetQueryableAsync();

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                queryable = queryable.Where(u =>
                    u.UserName.Contains(input.Filter) ||
                    u.Email.Contains(input.Filter) ||
                    (u.Name != null && u.Name.Contains(input.Filter)) ||
                    (u.Surname != null && u.Surname.Contains(input.Filter)));
            }

            // Filter by tenant - ensure tenant isolation
            if (_currentTenant.Id.HasValue)
            {
                // Non-host users: always restrict to own tenant, ignore input.TenantId/OnlyHost
                queryable = queryable.Where(u => u.TenantId == _currentTenant.Id.Value);
            }
            else if (input.OnlyHost == true)
            {
                // Host users: “全局”只看 host 用户
                queryable = queryable.Where(u => u.TenantId == null);
            }
            else if (input.TenantId.HasValue)
            {
                // Host users: filter by specified tenant
                queryable = queryable.Where(u => u.TenantId == input.TenantId.Value);
            }
            // Host users with no TenantId filter see all users

            var totalCount = await queryable.CountAsync();

            var users = await queryable
                .OrderByDescending(u => u.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            var dtos = new List<TenantUserDto>(users.Count);
            foreach (var user in users)
            {
                dtos.Add(await MapToDtoAsync(user));
            }

            return new PagedResultDto<TenantUserDto>(totalCount, dtos);
        }
    }

    public async Task<TenantUserDto> GetAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            CheckTenantOwnership(user);
            return await MapToDtoAsync(user);
        }
    }

    public async Task<List<string>> GetRolesForUserAsync(Guid userId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(userId);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {userId}");
            }

            CheckTenantOwnership(user);

            using (_currentTenant.Change(user.TenantId))
            {
                return (await _userManager.GetRolesAsync(user)).ToList();
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Users.Edit)]
    public async Task<TenantUserDto> UpdateAsync(Guid id, UpdateTenantUserDto input)
    {
        // ① 跨租户读取用户（host 管理员可编辑任意租户用户）
        Volo.Abp.Identity.IdentityUser user;
        using (DataFilter.Disable<IMultiTenant>())
        {
            user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            CheckTenantOwnership(user);

            // 租户用户不允许持有全局 LeagueAdmin 角色。
            EnsureNoLeagueAdminForTenantUser(user.TenantId, input.RoleNames);

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                throw new UserFriendlyException("名称不能为空");
            }
        }

        // ② 修改用户与角色的操作必须在“用户所属租户”的过滤上下文里执行：
        //    若仍在 DataFilter.Disable<IMultiTenant> 下 SetRolesAsync，RoleRepository.FindByNormalizedName
        //    会命中 host/其他租户的同名角色实例，导致旧角色移除失败（“改身份后旧身份仍在”）。
        using (_currentTenant.Change(user.TenantId))
        {
            (await _userManager.SetUserNameAsync(user, input.UserName))
                .CheckErrors();
            (await _userManager.SetEmailAsync(user, input.Email))
                .CheckErrors();
            user.Name = input.Name.Trim();
            if (!string.IsNullOrWhiteSpace(input.Surname))
            {
                user.Surname = input.Surname.Trim();
            }
            user.SetIsActive(input.IsActive);
            user.SetPhoneNumber(input.PhoneNumber, input.PhoneNumberConfirmed);

            if (input.MajorId.HasValue)
            {
                user.SetProperty(MajorIdExtraProperty, input.MajorId.Value);
                user.SetProperty("Major", null);
            }
            else
            {
                user.RemoveProperty(MajorIdExtraProperty);
                if (!string.IsNullOrWhiteSpace(input.Major))
                {
                    user.SetProperty("Major", input.Major.Trim());
                }
                else
                {
                    user.SetProperty("Major", null);
                }
            }

            // 未指定班级（既没传 ClassId，也没要求退班）时，保留原有班级文本，
            // 避免只改别的字段就把「班级」清空。班级关系本身不动。
            if (!input.ClassId.HasValue && !input.RemoveFromClass && input.ClassName == null)
            {
                input.ClassName = user.HasProperty("ClassName") ? user.GetProperty<string>("ClassName") : null;
            }

            // 应用用户资料扩展字段（工号 / 班级 / 年级 / 院校等）。
            // 应用只支持单用户角色：RoleType 与 RoleNames 里的唯一角色保持同步。
            var roleType = GetRoleTypeFromRoleName(input.RoleNames?.FirstOrDefault());
            // 与 SetOrRemoveProperty 同理，RoleType 也是映射列：清空需显式 SetProperty null。
            user.SetProperty("RoleType", roleType.HasValue ? (int)roleType.Value : (int?)null);

            ApplyProfileProperties(user, new ProfilePropertyValues
            {
                SchoolId = input.SchoolId,
                EmployeeNumber = input.EmployeeNumber,
                Department = input.Department,
                Course = input.Course,
                Title = input.Title,
                StudentNumber = input.StudentNumber,
                Grade = input.Grade,
                ClassName = input.ClassName,
                ManagementScope = input.ManagementScope,
                CompanyName = input.CompanyName,
                UnifiedSocialCreditCode = input.UnifiedSocialCreditCode,
                Position = input.Position,
                Industry = input.Industry,
                PartnerSchool = input.PartnerSchool,
                Remark = input.Remark,
            });

            (await _userManager.UpdateAsync(user))
                .CheckErrors();

            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                (await _userManager.ResetPasswordAsync(user, token, input.Password))
                    .CheckErrors();
            }

            if (input.RoleNames != null)
            {
                (await _userManager.SetRolesAsync(user, input.RoleNames))
                    .CheckErrors();

                // SetRolesAsync 内部已持久化；再次显式 Update 确保 RoleType 等扩展属性也落库。
                (await _userManager.UpdateAsync(user))
                    .CheckErrors();
            }

            // 学生归班 / 退班：同步「学生 ↔ 教学班」成员关系并回写班级名文本
            if (input.ClassId.HasValue || input.RemoveFromClass)
            {
                await SyncStudentClassAsync(user, input.ClassId);
            }

            return await MapToDtoAsync(user);
        }
    }

    [Authorize(KnowledgeHubPermissions.Users.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(id);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {id}");
            }

            CheckTenantOwnership(user);

            using (_currentTenant.Change(user.TenantId))
            {
                (await _userManager.DeleteAsync(user))
                    .CheckErrors();
            }
        }
    }

    /// <summary>
    /// Verify that the target user belongs to the same tenant as the current user.
    /// Host users can access any tenant's users.
    /// </summary>
    private void CheckTenantOwnership(Volo.Abp.Identity.IdentityUser user)
    {
        if (_currentTenant.Id.HasValue && user.TenantId != _currentTenant.Id)
        {
            throw new UserFriendlyException("您没有权限访问该租户的用户数据");
        }
    }

    /// <summary>
    /// 联盟管理员（LeagueAdmin）是全局角色（TenantId=null），租户内不应存在。
    /// 目标用户归属某租户时，拒绝分配 LeagueAdmin。
    /// </summary>
    private static void EnsureNoLeagueAdminForTenantUser(Guid? targetTenantId, IEnumerable<string>? roleNames)
    {
        if (!targetTenantId.HasValue || roleNames == null)
        {
            return;
        }

        if (roleNames.Any(r => string.Equals(r, "LeagueAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserFriendlyException("租户内不允许分配“联盟管理员”角色，联盟管理员为全局账号。");
        }

        // 单角色约束：本应用的学生端 / 教师端工作台按单一角色分发（登录后走 postLoginGuard），
        // 同时拥有学生+教师两个角色会导致工作台入口不确定（教师端反馈 ohw 同时是学生和教师）。
        // 这里在创建 / 编辑时直接拦住，从源头保证一人一角色。
        var distinctRoles = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctRoles.Count > 1)
        {
            throw new UserFriendlyException($"一个用户只能分配一个角色，当前选择了 {distinctRoles.Count} 个（{string.Join("、", distinctRoles)}），请只保留一个。");
        }
    }

    private async Task<TenantUserDto> MapToDtoAsync(Volo.Abp.Identity.IdentityUser user)
    {
        var dto = new TenantUserDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            UserName = user.UserName,
            Name = user.Name,
            Surname = user.Surname,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            IsActive = user.IsActive,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            ConcurrencyStamp = user.ConcurrencyStamp,
            CreationTime = user.CreationTime
        };
        foreach (var kv in user.ExtraProperties)
        {
            dto.SetProperty(kv.Key, kv.Value);
        }

        if (user.HasProperty(MajorIdExtraProperty))
        {
            var majorId = user.GetProperty<Guid?>(MajorIdExtraProperty);
            dto.MajorId = majorId;
            if (majorId.HasValue)
            {
                var major = await _majorRepository.FindAsync(majorId.Value);
                dto.MajorName = major?.Name;
            }
        }

        // 班级：以教学班成员关系为准（用户档案上的 ClassName 只是历史文本）
        var classInfo = await GetStudentClassAsync(user.Id);
        if (classInfo != null)
        {
            dto.ClassId = classInfo.ClassId;
            dto.ClassName = classInfo.ClassName;
        }

        return dto;
    }

    /// <summary>学生所属教学班（ClassId + 名称）。</summary>
    private sealed record StudentClassInfo(Guid ClassId, string ClassName);

    /// <summary>
    /// 取学生所属的教学班。
    ///
    /// <para>一个学生只归一个班：若历史数据里存在多条成员关系，取加入最早的一条。</para>
    /// </summary>
    private async Task<StudentClassInfo?> GetStudentClassAsync(Guid studentId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var memberQuery = await _classMemberRepository.GetQueryableAsync();
            var member = await memberQuery
                .Where(x => x.StudentId == studentId)
                .OrderBy(x => x.JoinedAt)
                .FirstOrDefaultAsync();

            if (member == null)
            {
                return null;
            }

            var cls = await _classRepository.FindAsync(member.ClassId);
            if (cls == null)
            {
                return null;
            }

            return new StudentClassInfo(cls.Id, cls.Name);
        }
    }

    /// <summary>
    /// 同步「学生 ↔ 教学班」成员关系，并回写用户档案上的班级名文本。
    ///
    /// <para>关系以教学班成员表为事实来源；用户档案上的 <c>ClassName</c> 文本同步回写，
    /// 兼容按班级文本检索的旧逻辑。</para>
    /// </summary>
    private async Task SyncStudentClassAsync(Volo.Abp.Identity.IdentityUser user, Guid? classId)
    {
        if (!user.TenantId.HasValue)
        {
            // host 全局账号不属于任何学校，没有班级概念
            return;
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var memberQuery = await _classMemberRepository.GetQueryableAsync();
            var members = await memberQuery.Where(x => x.StudentId == user.Id).ToListAsync();

            // 换班 / 退班：清掉旧关系
            var stale = members.Where(m => m.ClassId != classId).ToList();
            if (stale.Count > 0)
            {
                await _classMemberRepository.DeleteManyAsync(stale, autoSave: true);
            }

            string? className = null;
            if (classId.HasValue)
            {
                var cls = await _classRepository.FindAsync(classId.Value);
                if (cls == null)
                {
                    throw new UserFriendlyException("所选教学班不存在。");
                }

                if (cls.TenantId.HasValue && cls.TenantId != user.TenantId)
                {
                    throw new UserFriendlyException("不能把学生加入其他学校的教学班。");
                }

                if (cls.Status == TeachingClassStatus.Archived)
                {
                    throw new UserFriendlyException("已结课的班级不能再加入学生。");
                }

                className = cls.Name;

                if (!members.Any(m => m.ClassId == classId.Value))
                {
                    var fullName = string.Join(string.Empty, new[] { user.Name, user.Surname }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                    await _classMemberRepository.InsertAsync(
                        new TeachingClassMember(GuidGenerator.Create(), cls.Id, user.Id)
                        {
                            TenantId = cls.TenantId ?? user.TenantId,
                            StudentName = fullName,
                            StudentNumber = user.UserName,
                            Source = TeachingClassMemberSource.Manual,
                            JoinedAt = DateTime.UtcNow
                        },
                        autoSave: true);
                }
            }

            // 回写班级名文本（与 SetOrRemoveProperty 同样处理：空值要显式置 null）
            user.SetProperty("ClassName", className);
            await _userManager.UpdateAsync(user);
        }
    }
}
