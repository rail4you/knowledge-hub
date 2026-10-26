using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.Practicums;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Schools;
using KnowledgeHub.Schools.Dtos;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库管理端 AppService（院校管理员）。
///
/// <para>
/// 需求「院校管理员可以新建、删除资源库」「院校管理员可以分配租户内的教师到不同的私有资源库」
/// 「如果资源库是院校共建的，需要设置主学校，和关联学校」。
/// </para>
///
/// <para>
/// 数据模型：库是<b>主学校</b>的租户级实体；关联学校通过 <c>ResourceLibraryMember</c> 加入；
/// 教师分配通过 <c>ResourceLibraryTeacher</c> 记录。
/// </para>
///
/// <para>
/// 专业维度：库通过 <c>PrimaryMajorId</c> 可选绑定主建专业；留空表示不限专业，
/// 设置后挂进库的课程/资源/实训专业必须一致（由 <c>ResourceLibraryAttachmentValidator</c> 校验）。
/// </para>
/// </summary>
public class ResourceLibraryAppService : KnowledgeHubAppService, IResourceLibraryAppService
{
    /// <summary>院校管理员角色名（租户级）。</summary>
    public const string SchoolAdminRole = "SchoolAdmin";

    /// <summary>平台全局管理员角色名（host 级）。</summary>
    public const string AdminRole = "admin";

    /// <summary>教师角色名。</summary>
    public const string TeacherRole = "Teacher";

    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<ResourceLibraryMember, Guid> _memberRepository;
    private readonly IRepository<ResourceLibraryTeacher, Guid> _teacherRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<PracticumProject, Guid> _practicumRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IResourceLibraryAccessService _accessService;
    private readonly IDataFilter _dataFilter;

    public ResourceLibraryAppService(
        IResourceLibraryRepository libraryRepository,
        IRepository<ResourceLibraryMember, Guid> memberRepository,
        IRepository<ResourceLibraryTeacher, Guid> teacherRepository,
        ITenantRepository tenantRepository,
        IMajorRepository majorRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<PracticumProject, Guid> practicumRepository,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<IdentityRole, Guid> roleRepository,
        IResourceLibraryAccessService accessService,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _memberRepository = memberRepository;
        _teacherRepository = teacherRepository;
        _tenantRepository = tenantRepository;
        _majorRepository = majorRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _practicumRepository = practicumRepository;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _identityUserRepository = identityUserRepository;
        _roleRepository = roleRepository;
        _accessService = accessService;
        _dataFilter = dataFilter;
    }

    /// <summary>获取当前租户可见的资源库：主建的 + 参与共建的。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<List<ResourceLibraryDto>> GetListAsync(ResourceLibraryListRequestDto input)
    {
        var currentTenantId = CurrentTenant.Id;
        if (currentTenantId == null)
        {
            return new List<ResourceLibraryDto>();
        }

        var tenantId = currentTenantId.Value;
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var libraries = await _accessService.GetVisibleLibrariesAsync(tenantId, userId, isTenantAdmin);

        if (input?.PrimaryMajorId.HasValue == true)
        {
            libraries = libraries.Where(x => x.PrimaryMajorId == input.PrimaryMajorId.Value).ToList();
        }

        if (input?.Visibility.HasValue == true)
        {
            libraries = libraries.Where(x => x.Visibility == input.Visibility.Value).ToList();
        }

        if (input?.OnlyJoined == true)
        {
            libraries = libraries.Where(x => x.Members.Any(m => m.MemberTenantId == tenantId)).ToList();
        }

        var result = new List<ResourceLibraryDto>();
        foreach (var library in libraries
                     .OrderBy(x => x.SortOrder)
                     .ThenByDescending(x => x.CreationTime))
        {
            result.Add(await ToDtoAsync(library, tenantId, userId, isTenantAdmin));
        }

        return result;
    }

    /// <summary>获取资源库详情（主学校或共建方均可查看）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<ResourceLibraryDto> GetAsync(Guid id)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var target = await _accessService.GetLibraryAsync(id, currentTenantId, userId, isTenantAdmin);

        return await ToDtoAsync(target, currentTenantId, userId, isTenantAdmin);
    }

    /// <summary>创建教学资源库，当前租户自动成为主学校。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Create)]
    public virtual async Task<ResourceLibraryDto> CreateAsync(CreateResourceLibraryDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("请填写教学资源库名称。");
        }

        if (input.PrimaryMajorId.HasValue)
        {
            await EnsureMajorBelongsToTenantAsync(input.PrimaryMajorId.Value, currentTenantId);
        }

        var visibility = input.Visibility ?? ResourceLibraryVisibility.Private;
        var partnerTenantIds = await NormalizePartnerIdsAsync(input.PartnerTenantIds, currentTenantId);

        if (visibility == ResourceLibraryVisibility.CoBuilt && partnerTenantIds.Count == 0)
        {
            throw new UserFriendlyException("院校共建库必须至少设置一所关联学校。");
        }

        var library = new ResourceLibrary(GuidGenerator.Create(), currentTenantId, input.Name.Trim())
        {
            Description = input.Description,
            CoverUrl = input.CoverUrl,
            PrimaryMajorId = input.PrimaryMajorId,
            Principal = input.Principal,
            IsPublic = input.IsPublic ?? true,
            SortOrder = input.SortOrder ?? 0,
            Visibility = visibility,
            Status = ResourceLibraryStatus.Draft
        };

        await _libraryRepository.InsertAsync(library, autoSave: true);

        // 主学校自身作为 Owner 成员入表，保证「成员」语义统一
        var tenant = await _tenantRepository.FindAsync(currentTenantId);
        library.Members.Add(new ResourceLibraryMember(
            GuidGenerator.Create(),
            library.Id,
            currentTenantId,
            await ResolveTenantDisplayNameAsync(currentTenantId, tenant?.Name),
            ResourceLibraryMemberRole.Owner,
            currentTenantId));

        await _libraryRepository.UpdateAsync(library, autoSave: true);

        // 关联学校（仅院校共建库）
        foreach (var partnerTenantId in partnerTenantIds)
        {
            await AddPartnerMemberAsync(library, partnerTenantId, currentTenantId);
        }

        return await ToDtoAsync(library, currentTenantId, CurrentUser.Id, IsTenantAdmin());
    }

    /// <summary>更新教学资源库（仅主学校）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Edit)]
    public virtual async Task<ResourceLibraryDto> UpdateAsync(Guid id, UpdateResourceLibraryDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        if (input.Name != null)
        {
            library.SetName(input.Name);
        }

        if (input.Description != null)
        {
            library.SetDescription(input.Description);
        }

        if (input.CoverUrl != null)
        {
            library.SetCoverUrl(input.CoverUrl);
        }

        if (input.PrimaryMajorId.HasValue)
        {
            await EnsureMajorBelongsToTenantAsync(input.PrimaryMajorId.Value, currentTenantId);
            library.SetPrimaryMajorId(input.PrimaryMajorId.Value);
        }

        if (input.Principal != null)
        {
            library.SetPrincipal(input.Principal);
        }

        if (input.IsPublic.HasValue)
        {
            library.SetIsPublic(input.IsPublic.Value);
        }

        if (input.SortOrder.HasValue)
        {
            library.SetSortOrder(input.SortOrder.Value);
        }

        if (input.Status.HasValue)
        {
            library.SetStatus(input.Status.Value);
        }

        // 可见性变更：私有 / 公开 → 清空关联学校；院校共建 → 必须有关联学校
        if (input.Visibility.HasValue && input.Visibility.Value != library.Visibility)
        {
            var nextVisibility = input.Visibility.Value;

            if (nextVisibility == ResourceLibraryVisibility.CoBuilt)
            {
                var partners = input.PartnerTenantIds != null
                    ? await NormalizePartnerIdsAsync(input.PartnerTenantIds, currentTenantId)
                    : await GetExistingPartnerIdsAsync(library.Id, currentTenantId);

                if (partners.Count == 0)
                {
                    throw new UserFriendlyException("院校共建库必须至少设置一所关联学校。");
                }

                library.SetVisibility(nextVisibility);
                await ReplacePartnerMembersAsync(library, partners, currentTenantId);
            }
            else
            {
                library.SetVisibility(nextVisibility);
                await ClearPartnerMembersAsync(library.Id, currentTenantId);
            }
        }
        else if (input.Visibility.HasValue)
        {
            library.SetVisibility(input.Visibility.Value);
        }
        else if (input.PartnerTenantIds != null)
        {
            // 未改可见性但传了关联学校列表：仅院校共建库允许维护
            if (library.Visibility != ResourceLibraryVisibility.CoBuilt)
            {
                throw new UserFriendlyException("只有院校共建库才能设置关联学校。");
            }

            var partners = await NormalizePartnerIdsAsync(input.PartnerTenantIds, currentTenantId);
            await ReplacePartnerMembersAsync(library, partners, currentTenantId);
        }

        await _libraryRepository.UpdateAsync(library, autoSave: true);

        return await ToDtoAsync(library, currentTenantId, CurrentUser.Id, IsTenantAdmin());
    }

    /// <summary>删除教学资源库（仅主学校）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        // 库内资源/课程/实训解除归属，避免出现指向已删库的悬空引用
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resources = await AsyncExecuter.ToListAsync(
                (await _resourceRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            foreach (var resource in resources)
            {
                resource.LibraryId = null;
            }

            var courses = await AsyncExecuter.ToListAsync(
                (await _courseRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            foreach (var course in courses)
            {
                course.LibraryId = null;
            }

            var practicums = await AsyncExecuter.ToListAsync(
                (await _practicumRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            foreach (var practicum in practicums)
            {
                practicum.LibraryId = null;
            }

            if (resources.Count > 0)
            {
                await _resourceRepository.UpdateManyAsync(resources, autoSave: true);
            }

            if (courses.Count > 0)
            {
                await _courseRepository.UpdateManyAsync(courses, autoSave: true);
            }

            if (practicums.Count > 0)
            {
                await _practicumRepository.UpdateManyAsync(practicums, autoSave: true);
            }

            // 教师分配记录随库级联删除（EF 外键级联已配置，这里显式清理以兼容历史数据）
            var assignments = await AsyncExecuter.ToListAsync(
                (await _teacherRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            if (assignments.Count > 0)
            {
                await _teacherRepository.DeleteManyAsync(assignments, autoSave: true);
            }
        }

        await _libraryRepository.DeleteAsync(library, autoSave: true);
    }

    /// <summary>添加关联学校（仅主学校、仅院校共建库）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageMembers)]
    public virtual async Task<ResourceLibraryMemberDto> AddMemberAsync(Guid id, AddResourceLibraryMemberDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        if (library.Visibility != ResourceLibraryVisibility.CoBuilt)
        {
            throw new UserFriendlyException("只有院校共建库才能设置关联学校。");
        }

        if (input.MemberTenantId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择关联学校。");
        }

        if (input.MemberTenantId == currentTenantId)
        {
            throw new UserFriendlyException("主学校无需重复添加。");
        }

        var targetTenant = await _tenantRepository.FindAsync(input.MemberTenantId);
        if (targetTenant == null)
        {
            throw new UserFriendlyException("关联学校不存在。");
        }

        var members = await _libraryRepository.GetMembersAsync(library.Id);
        if (members.Any(x => x.MemberTenantId == input.MemberTenantId))
        {
            throw new UserFriendlyException("该学校已在共建名单中。");
        }

        var member = new ResourceLibraryMember(
            GuidGenerator.Create(),
            library.Id,
            input.MemberTenantId,
            await ResolveTenantDisplayNameAsync(input.MemberTenantId, targetTenant.Name),
            ResourceLibraryMemberRole.Partner,
            currentTenantId);

        await _memberRepository.InsertAsync(member, autoSave: true);

        return new ResourceLibraryMemberDto
        {
            Id = member.Id,
            MemberTenantId = member.MemberTenantId,
            MemberTenantName = member.MemberTenantName,
            Role = member.Role,
            JoinedTime = member.JoinedTime
        };
    }

    /// <summary>移除关联学校（仅主学校、仅院校共建库）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageMembers)]
    public virtual async Task RemoveMemberAsync(Guid id, Guid memberTenantId)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        if (library.Visibility != ResourceLibraryVisibility.CoBuilt)
        {
            throw new UserFriendlyException("只有院校共建库才能设置关联学校。");
        }

        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var member = members.FirstOrDefault(x => x.MemberTenantId == memberTenantId);
        if (member == null)
        {
            throw new UserFriendlyException("该学校不在共建名单中。");
        }

        if (member.Role == ResourceLibraryMemberRole.Owner)
        {
            throw new UserFriendlyException("不能移除主学校。");
        }

        await _memberRepository.DeleteAsync(member, autoSave: true);
    }

    /// <summary>获取资源库已分配的教师列表。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<List<ResourceLibraryTeacherDto>> GetTeachersAsync(Guid id)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await FindLibraryAsync(id);

        await EnsureManageableAsync(library, currentTenantId);

        var teachers = await _libraryRepository.GetTeachersAsync(library.Id);
        var result = new List<ResourceLibraryTeacherDto>(teachers.Count);

        foreach (var teacher in teachers)
        {
            result.Add(new ResourceLibraryTeacherDto
            {
                Id = teacher.Id,
                LibraryId = teacher.LibraryId,
                UserId = teacher.UserId,
                UserName = await ResolveUserNameAsync(teacher.UserId, teacher.UserName),
                TenantId = teacher.TenantId,
                TenantName = teacher.TenantId.HasValue
                    ? await ResolveTenantDisplayNameAsync(teacher.TenantId.Value, null)
                    : null,
                AssignedTime = teacher.AssignedTime
            });
        }

        return result;
    }

    /// <summary>把本校教师分配到教学资源库。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.AssignTeachers)]
    public virtual async Task<ResourceLibraryTeacherDto> AssignTeacherAsync(Guid id, AssignResourceLibraryTeacherDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await FindLibraryAsync(id);

        await EnsureManageableAsync(library, currentTenantId);

        if (input.UserId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择教师。");
        }

        // 只能分配本校教师 —— 跨校教师由对方学校的院校管理员分配
        var user = await FindUserAsync(input.UserId);
        if (user == null || user.TenantId != currentTenantId)
        {
            throw new UserFriendlyException("只能分配本校教师。");
        }

        var already = await _libraryRepository.GetTeachersAsync(library.Id);
        if (already.Any(x => x.UserId == input.UserId))
        {
            throw new UserFriendlyException("该教师已分配到此教学资源库。");
        }

        var assignment = new ResourceLibraryTeacher(
            GuidGenerator.Create(),
            library.Id,
            input.UserId,
            user.Name ?? user.UserName,
            currentTenantId);

        await _teacherRepository.InsertAsync(assignment, autoSave: true);

        return new ResourceLibraryTeacherDto
        {
            Id = assignment.Id,
            LibraryId = assignment.LibraryId,
            UserId = assignment.UserId,
            UserName = assignment.UserName,
            TenantId = assignment.TenantId,
            TenantName = await ResolveTenantDisplayNameAsync(currentTenantId, null),
            AssignedTime = assignment.AssignedTime
        };
    }

    /// <summary>取消教师的教学资源库分配。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.AssignTeachers)]
    public virtual async Task RemoveTeacherAsync(Guid id, Guid userId)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await FindLibraryAsync(id);

        await EnsureManageableAsync(library, currentTenantId);

        var teachers = await _libraryRepository.GetTeachersAsync(library.Id);
        var assignment = teachers.FirstOrDefault(x => x.UserId == userId);
        if (assignment == null)
        {
            throw new UserFriendlyException("该教师未分配到此教学资源库。");
        }

        await _teacherRepository.DeleteAsync(assignment, autoSave: true);
    }

    /// <summary>获取本校可分配的教师列表。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.AssignTeachers)]
    public virtual async Task<List<ResourceLibraryTeacherDto>> GetAssignableTeachersAsync(ResourceLibraryTeacherListRequestDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");

        // ABP 10 的用户-角色关联只存 RoleId，需要先按名称解析出「教师」角色 Id
        var teacherRoles = await _roleRepository.GetListAsync(x => x.Name == TeacherRole);
        var teacherRoleIds = teacherRoles.Select(r => r.Id).ToList();

        var query = await _identityUserRepository.GetQueryableAsync();
        var query2 = query.Where(u => u.TenantId == currentTenantId);

        if (teacherRoleIds.Count > 0)
        {
            query2 = query2.Where(u => u.Roles.Any(r => teacherRoleIds.Contains(r.RoleId)));
        }
        else
        {
            // 本校没有「教师」角色时返回空列表，而不是把全校教师都列出来
            return new List<ResourceLibraryTeacherDto>();
        }

        if (!string.IsNullOrWhiteSpace(input?.Filter))
        {
            var filter = input.Filter.Trim();
            query2 = query2.Where(u => u.Name.Contains(filter) || u.UserName.Contains(filter));
        }

        var users = await AsyncExecuter.ToListAsync(query2);

        // 已分配到目标库的用户需要标记出来
        HashSet<Guid> assigned = new();
        if (input?.LibraryId is Guid libraryId && libraryId != Guid.Empty)
        {
            var existing = await _libraryRepository.GetTeachersAsync(libraryId);
            assigned = existing.Select(x => x.UserId).ToHashSet();
        }

        var tenantName = await ResolveTenantDisplayNameAsync(currentTenantId, null);
        var result = new List<ResourceLibraryTeacherDto>();
        foreach (var user in users.OrderBy(x => x.Name))
        {
            result.Add(new ResourceLibraryTeacherDto
            {
                Id = Guid.Empty,
                LibraryId = input?.LibraryId ?? Guid.Empty,
                UserId = user.Id,
                UserName = user.Name ?? user.UserName,
                TenantId = user.TenantId,
                TenantName = tenantName,
                IsAssigned = assigned.Contains(user.Id),
                AssignedTime = default
            });
        }

        return result;
    }

    #region 私有辅助

    /// <summary>当前用户是否为本租户的院校管理员。</summary>
    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        if (roles == null)
        {
            return false;
        }

        return roles.Contains(SchoolAdminRole) || roles.Contains(AdminRole);
    }

    /// <summary>跨租户取库，并预加载共建成员（关联学校判定依赖它）。</summary>
    private async Task<ResourceLibrary> FindLibraryAsync(Guid id)
    {
        var library = await _libraryRepository.FindWithMembersAsync(id);
        if (library == null)
        {
            throw new UserFriendlyException("教学资源库不存在。");
        }

        return library;
    }

    /// <summary>取库并校验「主学校」身份（建库 / 改库 / 删库 / 设关联学校）。</summary>
    private async Task<ResourceLibrary> GetOwnedLibraryAsync(Guid id, Guid currentTenantId)
    {
        var library = await FindLibraryAsync(id);
        if (library.OwnerTenantId != currentTenantId)
        {
            throw new UserFriendlyException("只有主建学校可以管理该教学资源库。");
        }

        return library;
    }

    /// <summary>
    /// 校验当前租户是否可以管理该库：主学校可以；院校共建库的关联方也可以
    /// （需求「共建学校租户的资源库列表里可以看到共享资源库，也可以分配老师」）。
    /// </summary>
    private async Task EnsureManageableAsync(ResourceLibrary library, Guid currentTenantId)
    {
        if (library.OwnerTenantId == currentTenantId)
        {
            return;
        }

        if (library.Visibility == ResourceLibraryVisibility.CoBuilt &&
            library.Members.Any(m => m.MemberTenantId == currentTenantId))
        {
            return;
        }

        throw new UserFriendlyException("只有主建学校或共建方可以管理该教学资源库。");
    }

    /// <summary>规范化关联学校 Id 列表：去重、剔除主学校自身、剔除不存在的租户。</summary>
    private async Task<List<Guid>> NormalizePartnerIdsAsync(List<Guid>? partnerTenantIds, Guid currentTenantId)
    {
        var result = new List<Guid>();
        if (partnerTenantIds == null || partnerTenantIds.Count == 0)
        {
            return result;
        }

        foreach (var tenantId in partnerTenantIds.Distinct())
        {
            if (tenantId == Guid.Empty || tenantId == currentTenantId)
            {
                continue;
            }

            if (await _tenantRepository.FindAsync(tenantId) == null)
            {
                throw new UserFriendlyException($"关联学校不存在（{tenantId}）。");
            }

            result.Add(tenantId);
        }

        return result;
    }

    /// <summary>取当前库已有关联学校（剔除主学校）。</summary>
    private async Task<List<Guid>> GetExistingPartnerIdsAsync(Guid libraryId, Guid currentTenantId)
    {
        var members = await _libraryRepository.GetMembersAsync(libraryId);
        return members
            .Where(m => m.Role == ResourceLibraryMemberRole.Partner && m.MemberTenantId != currentTenantId)
            .Select(m => m.MemberTenantId)
            .Distinct()
            .ToList();
    }

    /// <summary>写入关联学校成员记录。</summary>
    private async Task AddPartnerMemberAsync(ResourceLibrary library, Guid partnerTenantId, Guid currentTenantId)
    {
        var tenant = await _tenantRepository.FindAsync(partnerTenantId);
        var member = new ResourceLibraryMember(
            GuidGenerator.Create(),
            library.Id,
            partnerTenantId,
            await ResolveTenantDisplayNameAsync(partnerTenantId, tenant?.Name),
            ResourceLibraryMemberRole.Partner,
            currentTenantId);

        await _memberRepository.InsertAsync(member, autoSave: true);
    }

    /// <summary>用给定列表整体替换关联学校成员。</summary>
    private async Task ReplacePartnerMembersAsync(ResourceLibrary library, List<Guid> partnerTenantIds, Guid currentTenantId)
    {
        var existing = await _libraryRepository.GetMembersAsync(library.Id);
        var existingPartners = existing
            .Where(m => m.Role == ResourceLibraryMemberRole.Partner)
            .ToList();

        var target = new HashSet<Guid>(partnerTenantIds);

        foreach (var member in existingPartners)
        {
            if (!target.Contains(member.MemberTenantId))
            {
                await _memberRepository.DeleteAsync(member, autoSave: true);
            }
        }

        var existingIds = existingPartners.Select(m => m.MemberTenantId).ToHashSet();
        foreach (var partnerTenantId in partnerTenantIds)
        {
            if (!existingIds.Contains(partnerTenantId))
            {
                await AddPartnerMemberAsync(library, partnerTenantId, currentTenantId);
            }
        }
    }

    /// <summary>清空关联学校成员（切回私有 / 公开时调用）。</summary>
    private async Task ClearPartnerMembersAsync(Guid libraryId, Guid currentTenantId)
    {
        var members = await _libraryRepository.GetMembersAsync(libraryId);
        var partners = members
            .Where(m => m.Role == ResourceLibraryMemberRole.Partner && m.MemberTenantId != currentTenantId)
            .ToList();

        foreach (var member in partners)
        {
            await _memberRepository.DeleteAsync(member, autoSave: true);
        }
    }

    private async Task EnsureMajorBelongsToTenantAsync(Guid majorId, Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var major = await _majorRepository.FindAsync(majorId);
            if (major == null)
            {
                throw new UserFriendlyException("主建专业不存在。");
            }

            if (major.TenantId != tenantId)
            {
                throw new UserFriendlyException("主建专业必须属于当前学校。");
            }
        }
    }

    /// <summary>
    /// 租户 → 中文机构名（学校档案名 → 专业库展示名 → 租户名）。
    /// 需求「机构名称要用中文」：关联学校 / 主建方不能显示 qidi 这类租户 slug。
    /// </summary>
    private async Task<string> ResolveTenantDisplayNameAsync(Guid tenantId, string? fallback)
    {
        // 学校档案名优先：那是「这所学校」的权威中文机构名。
        // 注意不能拿 fallback 做等值排除 —— 历史成员记录里存的往往就是学校档案名，
        // 一旦等值就漏掉学校名、错误地回退到专业库展示名。
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school != null && !string.IsNullOrWhiteSpace(school.Name))
        {
            return school.Name;
        }

        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (!string.IsNullOrWhiteSpace(tenantInfo?.Name))
        {
            return tenantInfo.Name;
        }

        return fallback ?? tenantId.ToString();
    }

    /// <summary>批量解析成员显示名（历史记录里存的是租户 slug）。</summary>
    private async Task<List<ResourceLibraryMemberDto>> ResolveMemberNamesAsync(List<ResourceLibraryMember> members)
    {
        var result = new List<ResourceLibraryMemberDto>();
        foreach (var m in members)
        {
            result.Add(new ResourceLibraryMemberDto
            {
                Id = m.Id,
                MemberTenantId = m.MemberTenantId,
                MemberTenantName = await ResolveTenantDisplayNameAsync(m.MemberTenantId, m.MemberTenantName),
                Role = m.Role,
                JoinedTime = m.JoinedTime
            });
        }

        return result;
    }

    /// <summary>按 Id 取用户；不存在时返回 null（而不是抛 EntityNotFoundException）。</summary>
    private async Task<IdentityUser?> FindUserAsync(Guid userId)
    {
        var users = await _identityUserRepository.GetListAsync(x => x.Id == userId);
        return users.FirstOrDefault();
    }

    /// <summary>取教师姓名（优先库内快照，缺失时回查身份库）。</summary>
    private async Task<string?> ResolveUserNameAsync(Guid userId, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        var user = await FindUserAsync(userId);
        return user?.Name ?? user?.UserName;
    }

    private async Task<ResourceLibraryDto> ToDtoAsync(
        ResourceLibrary library,
        Guid currentTenantId,
        Guid? currentUserId,
        bool isTenantAdmin)
    {
        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var ownerTenant = await _tenantRepository.FindAsync(library.OwnerTenantId);

        string? majorName = null;
        if (library.PrimaryMajorId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var major = await _majorRepository.FindAsync(library.PrimaryMajorId.Value);
                majorName = major?.Name;
            }
        }

        var teacherCount = (await _libraryRepository.GetTeachersAsync(library.Id)).Count;

        // 同步内存中的共建成员：更新可见性 / 关联学校后，实体上的集合可能已过期，
        // 而 CanView / CanContribute 的判定依赖它。
        library.Members.Clear();
        foreach (var member in members)
        {
            library.Members.Add(member);
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resourceCount = await AsyncExecuter.CountAsync(
                (await _resourceRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            var courseCount = await AsyncExecuter.CountAsync(
                (await _courseRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            var practicumCount = await AsyncExecuter.CountAsync(
                (await _practicumRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));

            var currentMember = members.FirstOrDefault(x => x.MemberTenantId == currentTenantId);

            var isAssigned = currentUserId.HasValue &&
                             await _accessService.IsAssignedTeacherAsync(library.Id, currentUserId.Value);

            return new ResourceLibraryDto
            {
                Id = library.Id,
                TenantId = library.TenantId,
                Name = library.Name,
                Description = library.Description,
                CoverUrl = library.CoverUrl,
                PrimaryMajorId = library.PrimaryMajorId,
                PrimaryMajorName = majorName,
                OwnerTenantId = library.OwnerTenantId,
                OwnerTenantName = await ResolveTenantDisplayNameAsync(library.OwnerTenantId, ownerTenant?.Name),
                Principal = library.Principal,
                Status = library.Status,
                Visibility = library.Visibility,
                IsPublic = library.IsPublic,
                SortOrder = library.SortOrder,
                ResourceCount = resourceCount,
                CourseCount = courseCount,
                PracticumCount = practicumCount,
                MemberCount = members.Count,
                TeacherCount = teacherCount,
                CurrentRole = currentMember != null ? (int)currentMember.Role : -1,
                IsOwner = library.OwnerTenantId == currentTenantId,
                IsAssigned = isAssigned,
                CanView = await _accessService.CanViewAsync(library, currentTenantId, currentUserId, isTenantAdmin),
                CanContribute = await _accessService.CanContributeAsync(library, currentTenantId, currentUserId, isTenantAdmin),
                Members = await ResolveMemberNamesAsync(members)
            };
        }
    }

    #endregion
}
