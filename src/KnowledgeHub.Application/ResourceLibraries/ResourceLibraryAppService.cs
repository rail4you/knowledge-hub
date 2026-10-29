using System;
using Microsoft.AspNetCore.Mvc;
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
[IgnoreAntiforgeryToken]
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
    /// <summary>课程—资源库关联表（计算「库内有课程」用于实训挂库限制）。</summary>
    private readonly IRepository<ResourceLibraryCourse, Guid> _libraryCourseRepository;
    /// <summary>课程—专业关联表（专业资源库的默认教师来源）。</summary>
    private readonly IRepository<KnowledgeHub.Courses.CourseMajor, Guid> _courseMajorRepository;
    /// <summary>资源—资源库关联表（多对多；库内资源数量以「主库字段 + 关联表」的并集为准）。</summary>
    private readonly IRepository<ResourceLibraryResource, Guid> _libraryResourceRepository;
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
    /// <summary>社会学校（自行注册社会学员的归属租户）——不能作为参建单位。</summary>
    private readonly IPublicSchoolProvider _publicSchoolProvider;

    public ResourceLibraryAppService(
        IResourceLibraryRepository libraryRepository,
        IRepository<ResourceLibraryMember, Guid> memberRepository,
        IRepository<ResourceLibraryTeacher, Guid> teacherRepository,
        IRepository<ResourceLibraryCourse, Guid> libraryCourseRepository,
        IRepository<KnowledgeHub.Courses.CourseMajor, Guid> courseMajorRepository,
        IRepository<ResourceLibraryResource, Guid> libraryResourceRepository,
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
        IDataFilter dataFilter,
        IPublicSchoolProvider publicSchoolProvider)
    {
        _libraryRepository = libraryRepository;
        _memberRepository = memberRepository;
        _teacherRepository = teacherRepository;
        _libraryCourseRepository = libraryCourseRepository;
        _courseMajorRepository = courseMajorRepository;
        _libraryResourceRepository = libraryResourceRepository;
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
        _publicSchoolProvider = publicSchoolProvider;
    }

    /// <summary>获取当前租户可见的资源库：主建的 + 参与共建的。</summary>
    /// <summary>
    /// 「可关联的资源库」选项（课程 / 实训表单的多选表格数据源）。
    ///
    /// <para>范围：本校主建的库 + 本校参与共建的库。<paramref name="requireCourse"/> 为 true 时
    /// 只保留「库内已有课程」的库（实训项目与课程相关，只能挂在有课程的库）；
    /// 指定 <paramref name="courseId"/> 时进一步收窄为包含该课程的库。</para>
    /// </summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<List<LibraryAttachOptionDto>> GetAttachableOptionsAsync(
        bool requireCourse = false,
        Guid? courseId = null)
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            return new List<LibraryAttachOptionDto>();
        }

        // 库内课程：关联表 ∪ 主库字段（老数据只有 Course.LibraryId）
        var courseIdsByLibrary = new Dictionary<Guid, HashSet<Guid>>();

        void AddCourse(Guid libraryId, Guid courseIdValue)
        {
            if (!courseIdsByLibrary.TryGetValue(libraryId, out var set))
            {
                set = new HashSet<Guid>();
                courseIdsByLibrary[libraryId] = set;
            }
            set.Add(courseIdValue);
        }

        // 可选库范围与「资源库列表 / 教师端」保持同一套可见性：
        // 教师只拿到被分配到的公开/共建库 + 本专业私有库；院校管理员拿到本租户全部库。
        var scoped = (await _accessService.GetVisibleLibrariesAsync(
                tenantId.Value,
                CurrentUser.Id,
                IsTenantAdmin()))
            .OrderBy(l => l.Name)
            .ToList();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var linkQuery = await _libraryCourseRepository.GetQueryableAsync();
            foreach (var link in await AsyncExecuter.ToListAsync(linkQuery))
            {
                AddCourse(link.LibraryId, link.CourseId);
            }

            var courseQuery = await _courseRepository.GetQueryableAsync();
            var legacy = await AsyncExecuter.ToListAsync(
                courseQuery.Where(x => x.LibraryId.HasValue).Select(x => new { LibraryId = x.LibraryId!.Value, x.Id }));
            foreach (var item in legacy)
            {
                AddCourse(item.LibraryId, item.Id);
            }
        }

        var result = new List<LibraryAttachOptionDto>();
        foreach (var library in scoped)
        {
            var hasCourses = courseIdsByLibrary.TryGetValue(library.Id, out var courses);
            var courseCount = hasCourses ? courses!.Count : 0;

            if (requireCourse && courseCount == 0)
            {
                continue;
            }

            if (courseId.HasValue && (!hasCourses || !courses!.Contains(courseId.Value)))
            {
                continue;
            }

            result.Add(new LibraryAttachOptionDto
            {
                Id = library.Id,
                Name = library.Name,
                Visibility = (int)library.Visibility,
                CourseCount = courseCount
            });
        }

        return result;
    }

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
        var ordered = libraries
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreationTime)
            .ToList();

        // 批量预取：避免「每个库 12+ 次查询」的 N+1（库数一多列表页明显变慢）
        var batch = await BuildLibraryBatchAsync(ordered, userId);

        foreach (var library in ordered)
        {
            result.Add(await ToDtoAsync(library, batch, tenantId, userId, isTenantAdmin));
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

    /// <summary>
    /// 候选参建学校：当前租户（主建学校）之外的全部学校。
    ///
    /// <para>供建库 / 改库时选择「关联学校」。刻意不走门户的公开租户列表 ——
    /// 那个接口在学校域名下只返回本校，跨校选参建单位会拿不到数据。</para>
    ///
    /// <para>「社会学校」是平台给自行注册的社会学员用的归属租户，不是建制学校，
    /// 不能作为教学资源库的参建单位，这里排除掉。</para>
    /// </summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<List<ResourceLibraryPartnerTenantDto>> GetCandidatePartnerTenantsAsync()
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var publicSchoolTenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();

        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<ResourceLibraryPartnerTenantDto>();
        foreach (var tenant in tenants
                     .Where(t => t.Id != currentTenantId && t.Id != publicSchoolTenantId)
                     .OrderBy(t => t.Name))
        {
            result.Add(new ResourceLibraryPartnerTenantDto
            {
                Id = tenant.Id,
                Name = await ResolveTenantDisplayNameAsync(tenant.Id, tenant.Name),
                TenantName = tenant.Name
            });
        }

        return result;
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
            // 「对外开放自学」默认按形态推导：校级私有库不对外，共享库 / 共建库对外
            OpenToExternalLearners = input.OpenToExternalLearners ?? visibility.DefaultOpenToExternalLearners(),
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

        if (input.OpenToExternalLearners.HasValue)
        {
            // 校级私有库按需求不对外开放自学，禁止开启
            if (input.OpenToExternalLearners.Value && library.Visibility == ResourceLibraryVisibility.Private)
            {
                throw new UserFriendlyException("校级私有教学资源库不对外开放自学，无需开启该选项。");
            }

            library.SetOpenToExternalLearners(input.OpenToExternalLearners.Value);
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

            // 形态变化时，「对外开放自学」跟随形态默认值（除非请求中显式指定）
            if (!input.OpenToExternalLearners.HasValue)
            {
                library.SetOpenToExternalLearners(nextVisibility.DefaultOpenToExternalLearners());
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

        // 社会学校是自行注册社会学员的归属租户，不是建制学校，不能参与校际共建
        if (input.MemberTenantId == await _publicSchoolProvider.GetPublicSchoolTenantIdAsync())
        {
            throw new UserFriendlyException("社会学校不是建制学校，不能作为关联学校。");
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

        // 批量解析：避免「每个教师 1 次用户查询 + 每所学校 2 次名称查询」的 N+1（分配教师弹窗卡顿的主因）
        // 注意：没有显式分配时也要继续往下走 —— 专业资源库还要补「主建专业默认教师」。
        var missingNameUserIds = teachers
            .Where(x => string.IsNullOrWhiteSpace(x.UserName))
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var userNames = new Dictionary<Guid, string>();
        if (missingNameUserIds.Count > 0)
        {
            // 查看他校共建库时，教师可能属于别的租户：跨租户批量解析姓名
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var userQuery = await _identityUserRepository.GetQueryableAsync();
                var users = await AsyncExecuter.ToListAsync(
                    userQuery.Where(u => missingNameUserIds.Contains(u.Id))
                             .Select(u => new { u.Id, u.Name, u.UserName }));
                foreach (var user in users)
                {
                    userNames[user.Id] = user.Name ?? user.UserName;
                }
            }
        }

        var tenantIds = teachers
            .Where(x => x.TenantId.HasValue)
            .Select(x => x.TenantId!.Value)
            .Distinct()
            .ToList();
        var tenantNames = await ResolveTenantNamesAsync(tenantIds);

        var result = teachers.Select(teacher => new ResourceLibraryTeacherDto
        {
            Id = teacher.Id,
            LibraryId = teacher.LibraryId,
            UserId = teacher.UserId,
            UserName = !string.IsNullOrWhiteSpace(teacher.UserName)
                ? teacher.UserName
                : (userNames.TryGetValue(teacher.UserId, out var name) ? name : null),
            TenantId = teacher.TenantId,
            TenantName = teacher.TenantId.HasValue && tenantNames.TryGetValue(teacher.TenantId.Value, out var tenantName)
                ? tenantName
                : null,
            AssignedTime = teacher.AssignedTime
        }).ToList();

        // 专业资源库：默认由主建专业的全部教师负责，弹窗里也要能与之对应
        // （否则列表显示「已分配教师 N 人」，弹窗却是 0 人）。这类记录标记 IsImplicit，只读展示。
        var majorTeacherIds = await GetMajorTeacherIdsAsync(library.PrimaryMajorId, library.OwnerTenantId);
        var existingIds = result.Select(x => x.UserId).ToHashSet();
        var implicitIds = majorTeacherIds.Where(id => !existingIds.Contains(id)).ToList();

        if (implicitIds.Count > 0)
        {
            var implicitUsers = new List<ResourceLibraryTeacherDto>();

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var users = await AsyncExecuter.ToListAsync(
                    (await _identityUserRepository.GetQueryableAsync())
                        .Where(u => implicitIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.Name, u.UserName, u.TenantId }));

                var implicitTenantIds = users
                    .Where(x => x.TenantId.HasValue)
                    .Select(x => x.TenantId!.Value)
                    .Distinct()
                    .ToList();
                var implicitTenantNames = await ResolveTenantNamesAsync(implicitTenantIds);

                implicitUsers = users.Select(u => new ResourceLibraryTeacherDto
                {
                    Id = Guid.Empty,
                    LibraryId = library.Id,
                    UserId = u.Id,
                    UserName = u.Name ?? u.UserName,
                    TenantId = u.TenantId,
                    TenantName = u.TenantId.HasValue && implicitTenantNames.TryGetValue(u.TenantId.Value, out var tn)
                        ? tn
                        : null,
                    AssignedTime = default,
                    IsAssigned = true,
                    IsImplicit = true
                }).ToList();
            }

            result.AddRange(implicitUsers);
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

        // 共建库里会有其它学校的教师：只能由对方学校的院校管理员移除，本校只读展示
        if (assignment.TenantId.HasValue && assignment.TenantId.Value != currentTenantId)
        {
            throw new UserFriendlyException("只能移除本校教师；其它学校教师由对方学校管理员维护。");
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

        // 只取必要字段 + 数据库侧排序限量：本校教师量级大时避免整表加载与内存排序
        var users = await AsyncExecuter.ToListAsync(
            query2.OrderBy(u => u.Name)
                  .Select(u => new { u.Id, u.Name, u.UserName, u.TenantId })
                  .Take(500));

        // 已在库内（显式分配 ∪ 主建专业默认教师）的用户需要标记出来：
        // 用轻量查询直接取 Id 集合，避免为此再走一遍「已分配教师」的完整组装（含姓名/学校名解析）。
        HashSet<Guid> assigned = new();
        if (input?.LibraryId is Guid libraryId && libraryId != Guid.Empty)
        {
            var existing = await _libraryRepository.GetTeachersAsync(libraryId);
            assigned = existing.Select(x => x.UserId).ToHashSet();

            var library = await FindLibraryAsync(libraryId);
            foreach (var teacherId in await GetMajorTeacherIdsAsync(library))
            {
                assigned.Add(teacherId);
            }
        }

        // 当前租户名称只查一次（不是每个教师一次）
        var tenantNames = await ResolveTenantNamesAsync(new List<Guid> { currentTenantId });
        tenantNames.TryGetValue(currentTenantId, out var tenantName);

        return users.Select(user => new ResourceLibraryTeacherDto
        {
            Id = Guid.Empty,
            LibraryId = input?.LibraryId ?? Guid.Empty,
            UserId = user.Id,
            UserName = user.Name ?? user.UserName,
            TenantId = user.TenantId,
            TenantName = tenantName,
            IsAssigned = assigned.Contains(user.Id),
            AssignedTime = default
        }).ToList();
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

    /// <summary>
    /// 取某个专业下的全部教师 Id：以其所授课程的专业归属推导
    /// （`Course.MajorId` 主专业 ∪ `CourseMajor` 兼属专业）。
    /// </summary>
    /// <summary>库的「主建专业默认教师」Id 集合（限库归属学校）。</summary>
    private async Task<List<Guid>> GetMajorTeacherIdsAsync(ResourceLibrary library)
    {
        return await GetMajorTeacherIdsAsync(library.PrimaryMajorId, library.OwnerTenantId);
    }

    private async Task<List<Guid>> GetMajorTeacherIdsAsync(Guid? majorId, Guid ownerTenantId)
    {
        if (!majorId.HasValue)
        {
            return new List<Guid>();
        }

        var target = majorId.Value;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var courseQuery = await _courseRepository.GetQueryableAsync();
            var majorLinkQuery = await _courseMajorRepository.GetQueryableAsync();

            // 只算「库归属学校」的教师：别校同专业教师不属于本库的默认负责人
            var teacherIds = await AsyncExecuter.ToListAsync(
                courseQuery.Where(x => x.TenantId == ownerTenantId &&
                                       x.TeacherId.HasValue &&
                                       (x.MajorId == target ||
                                        majorLinkQuery.Any(m => m.CourseId == x.Id && m.MajorId == target)))
                           .Select(x => x.TeacherId!.Value));

            return teacherIds.Distinct().ToList();
        }
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

    /// <summary>
    /// 规范化关联学校 Id 列表：去重、剔除主学校自身、剔除社会学校、剔除不存在的租户。
    /// </summary>
    private async Task<List<Guid>> NormalizePartnerIdsAsync(List<Guid>? partnerTenantIds, Guid currentTenantId)
    {
        var result = new List<Guid>();
        if (partnerTenantIds == null || partnerTenantIds.Count == 0)
        {
            return result;
        }

        // 社会学校是自行注册社会学员的归属租户，不是建制学校，不能参与校际共建
        var publicSchoolTenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();

        foreach (var tenantId in partnerTenantIds.Distinct())
        {
            if (tenantId == Guid.Empty || tenantId == currentTenantId)
            {
                continue;
            }

            if (tenantId == publicSchoolTenantId)
            {
                throw new UserFriendlyException("社会学校不是建制学校，不能作为关联学校。");
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
    /// <summary>
    /// 批量组装成员 DTO（租户显示名从批量预取的结果里取，不再逐个查库）。
    /// </summary>
    private List<ResourceLibraryMemberDto> ResolveMemberNamesAsync(List<ResourceLibraryMember> members, LibraryBatch batch)
    {
        var result = new List<ResourceLibraryMemberDto>();
        foreach (var m in members)
        {
            result.Add(new ResourceLibraryMemberDto
            {
                Id = m.Id,
                MemberTenantId = m.MemberTenantId,
                MemberTenantName = batch.ResolveTenantName(m.MemberTenantId, m.MemberTenantName),
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

    /// <summary>
    /// 批量解析租户显示名（学校档案名 → 专业库展示名 → 租户 Id）。
    /// 每个租户 2 次查询，但只按「去重后的租户集合」执行，避免逐条 N+1。
    /// </summary>
    private async Task<Dictionary<Guid, string>> ResolveTenantNamesAsync(List<Guid> tenantIds)
    {
        var result = new Dictionary<Guid, string>();
        if (tenantIds.Count == 0)
        {
            return result;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var schools = await AsyncExecuter.ToListAsync(
                (await _schoolRepository.GetQueryableAsync()).Where(x => tenantIds.Contains(x.TenantId)));
            var infos = await AsyncExecuter.ToListAsync(
                (await _tenantInfoRepository.GetQueryableAsync()).Where(x => tenantIds.Contains(x.TenantId)));

            var schoolMap = schools
                .GroupBy(x => x.TenantId)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var infoMap = infos
                .GroupBy(x => x.TenantId)
                .ToDictionary(g => g.Key, g => g.First().Name);

            foreach (var tenantId in tenantIds)
            {
                if (schoolMap.TryGetValue(tenantId, out var schoolName) && !string.IsNullOrWhiteSpace(schoolName))
                {
                    result[tenantId] = schoolName;
                }
                else if (infoMap.TryGetValue(tenantId, out var infoName) && !string.IsNullOrWhiteSpace(infoName))
                {
                    result[tenantId] = infoName;
                }
                else
                {
                    result[tenantId] = tenantId.ToString();
                }
            }
        }

        return result;
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

    /// <summary>
    /// 资源库列表 / 详情共用的批量数据。
    ///
    /// <para>
    /// 之前是「每个库 12 次左右查询」（成员、租户名、专业名、教师分配、专业教师扫描、
    /// 关联资源、三类计数、分配判定……），库一多就是 N+1，列表页明显变慢。
    /// 这里把所有能批量的一次性取好，总查询数与库数量无关。
    /// </para>
    /// </summary>
    private sealed class LibraryBatch
    {
        /// <summary>共建成员（按库）。</summary>
        public Dictionary<Guid, List<ResourceLibraryMember>> Members { get; } = new();

        /// <summary>显式分配的教师 Id（按库）。</summary>
        public Dictionary<Guid, List<Guid>> AssignedTeacherIds { get; } = new();

        /// <summary>主建专业的默认教师 Id（按库）。</summary>
        public Dictionary<Guid, HashSet<Guid>> MajorTeacherIds { get; } = new();

        /// <summary>资源—库关联的资源 Id（按库）。</summary>
        public Dictionary<Guid, List<Guid>> LinkedResourceIds { get; } = new();

        public Dictionary<Guid, int> ResourceCounts { get; } = new();
        public Dictionary<Guid, int> CourseCounts { get; } = new();
        public Dictionary<Guid, int> PracticumCounts { get; } = new();

        /// <summary>专业名（按专业 Id）。</summary>
        public Dictionary<Guid, string?> MajorNames { get; } = new();

        /// <summary>租户显示名（学校档案名 → 租户信息名 → 租户名）。</summary>
        public Dictionary<Guid, string?> TenantNames { get; } = new();

        /// <summary>当前用户被显式分配到的库。</summary>
        public HashSet<Guid> UserAssignedLibraries { get; } = new();

        public int GetCount(Dictionary<Guid, int> source, Guid libraryId)
        {
            return source.TryGetValue(libraryId, out var count) ? count : 0;
        }

        /// <summary>「已分配教师」人数：显式分配 ∪ 主建专业默认教师。</summary>
        public int CountTeachers(ResourceLibrary library)
        {
            var ids = new HashSet<Guid>();

            if (AssignedTeacherIds.TryGetValue(library.Id, out var assigned))
            {
                foreach (var id in assigned)
                {
                    ids.Add(id);
                }
            }

            if (MajorTeacherIds.TryGetValue(library.Id, out var majorTeachers))
            {
                foreach (var id in majorTeachers)
                {
                    ids.Add(id);
                }
            }

            return ids.Count;
        }

        /// <summary>租户显示名（找不到时用传入的兜底值）。</summary>
        public string ResolveTenantName(Guid tenantId, string? fallback)
        {
            if (TenantNames.TryGetValue(tenantId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                return name!;
            }

            return fallback ?? tenantId.ToString();
        }
    }

    /// <summary>
    /// 一次性预取资源库列表所需的全部关联数据（消除 N+1）。
    /// </summary>
    private async Task<LibraryBatch> BuildLibraryBatchAsync(List<ResourceLibrary> libraries, Guid? currentUserId)
    {
        var batch = new LibraryBatch();
        if (libraries.Count == 0)
        {
            return batch;
        }

        var libraryIds = libraries.Select(x => x.Id).ToList();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            // ① 共建成员（成员学校可能是别校 → 跨租户）
            var members = await AsyncExecuter.ToListAsync(
                (await _memberRepository.GetQueryableAsync()).Where(x => libraryIds.Contains(x.LibraryId)));
            foreach (var group in members.GroupBy(x => x.LibraryId))
            {
                batch.Members[group.Key] = group.ToList();
            }

            // ② 显式分配的教师（教师可能来自关联学校 → 跨租户）
            var assignments = await AsyncExecuter.ToListAsync(
                (await _teacherRepository.GetQueryableAsync())
                    .Where(x => libraryIds.Contains(x.LibraryId))
                    .Select(x => new { x.LibraryId, x.UserId }));
            foreach (var group in assignments.GroupBy(x => x.LibraryId))
            {
                batch.AssignedTeacherIds[group.Key] = group.Select(x => x.UserId).Distinct().ToList();
            }

            // ③ 资源—库关联（多对多）
            var links = await AsyncExecuter.ToListAsync(
                (await _libraryResourceRepository.GetQueryableAsync())
                    .Where(x => libraryIds.Contains(x.LibraryId))
                    .Select(x => new { x.LibraryId, x.ResourceId }));
            foreach (var group in links.GroupBy(x => x.LibraryId))
            {
                batch.LinkedResourceIds[group.Key] = group.Select(x => x.ResourceId).Distinct().ToList();
            }

            // ④ 资源 / 课程 / 实训计数
            var resourceLibraryIds = await AsyncExecuter.ToListAsync(
                (await _resourceRepository.GetQueryableAsync())
                    .Where(x => x.LibraryId.HasValue && libraryIds.Contains(x.LibraryId.Value))
                    .Select(x => x.LibraryId!.Value));
            foreach (var group in resourceLibraryIds.GroupBy(x => x))
            {
                batch.ResourceCounts[group.Key] = group.Count();
            }

            // 关联资源：资源自身的主库字段与链接库不同时才额外计数
            // （与旧口径「主库字段 ∪ 关联表」一致，且同一资源在同一库只算一次）
            var linkedResourceIds = batch.LinkedResourceIds.Values.SelectMany(x => x).Distinct().ToList();
            if (linkedResourceIds.Count > 0)
            {
                var linkedOwn = new Dictionary<Guid, Guid?>();
                foreach (var chunk in linkedResourceIds.Chunk(1000))
                {
                    var rows = await AsyncExecuter.ToListAsync(
                        (await _resourceRepository.GetQueryableAsync())
                            .Where(x => chunk.Contains(x.Id))
                            .Select(x => new { x.Id, x.LibraryId }));
                    foreach (var row in rows)
                    {
                        linkedOwn[row.Id] = row.LibraryId;
                    }
                }

                foreach (var group in links.GroupBy(x => x.LibraryId))
                {
                    var extra = group.Select(x => x.ResourceId)
                        .Distinct()
                        .Count(id => linkedOwn.TryGetValue(id, out var own) && own != group.Key);
                    if (extra > 0)
                    {
                        batch.ResourceCounts[group.Key] = batch.ResourceCounts.GetValueOrDefault(group.Key) + extra;
                    }
                }
            }

            var courseLibraryIds = await AsyncExecuter.ToListAsync(
                (await _courseRepository.GetQueryableAsync())
                    .Where(x => x.LibraryId.HasValue && libraryIds.Contains(x.LibraryId.Value))
                    .Select(x => x.LibraryId!.Value));
            foreach (var group in courseLibraryIds.GroupBy(x => x))
            {
                batch.CourseCounts[group.Key] = group.Count();
            }

            var practicumLibraryIds = await AsyncExecuter.ToListAsync(
                (await _practicumRepository.GetQueryableAsync())
                    .Where(x => x.LibraryId.HasValue && libraryIds.Contains(x.LibraryId.Value))
                    .Select(x => x.LibraryId!.Value));
            foreach (var group in practicumLibraryIds.GroupBy(x => x))
            {
                batch.PracticumCounts[group.Key] = group.Count();
            }

            // ⑤ 当前用户被分配到的库（跨租户）
            if (currentUserId.HasValue)
            {
                var userLibraryIds = await AsyncExecuter.ToListAsync(
                    (await _teacherRepository.GetQueryableAsync())
                        .Where(x => x.UserId == currentUserId.Value)
                        .Select(x => x.LibraryId));
                foreach (var id in userLibraryIds)
                {
                    batch.UserAssignedLibraries.Add(id);
                }
            }
        }

        // ⑥ 主建专业默认教师（按「归属租户 + 专业」批量取，避免逐库扫全表）
        var majorPairs = libraries
            .Where(x => x.PrimaryMajorId.HasValue)
            .Select(x => (TenantId: x.OwnerTenantId, MajorId: x.PrimaryMajorId!.Value))
            .Distinct()
            .ToList();
        if (majorPairs.Count > 0)
        {
            var majorIds = majorPairs.Select(x => x.MajorId).Distinct().ToList();
            var ownerTenantIds = majorPairs.Select(x => x.TenantId).Distinct().ToList();

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var courseQuery = await _courseRepository.GetQueryableAsync();
                var linkQuery = await _courseMajorRepository.GetQueryableAsync();

                // 主专业
                var primaryRows = await AsyncExecuter.ToListAsync(
                    courseQuery.Where(x => x.TenantId.HasValue &&
                                           ownerTenantIds.Contains(x.TenantId.Value) &&
                                           x.TeacherId.HasValue &&
                                           x.MajorId.HasValue &&
                                           majorIds.Contains(x.MajorId.Value))
                               .Select(x => new { x.TenantId, x.MajorId, TeacherId = x.TeacherId!.Value }));

                // 兼属专业
                var secondaryRows = await AsyncExecuter.ToListAsync(
                    linkQuery.Where(m => majorIds.Contains(m.MajorId))
                             .Join(courseQuery, m => m.CourseId, c => c.Id, (m, c) => new { c.TenantId, c.MajorId, c.TeacherId })
                             .Where(x => x.TenantId.HasValue &&
                                         ownerTenantIds.Contains(x.TenantId.Value) &&
                                         x.TeacherId.HasValue)
                             .Select(x => new { x.TenantId, x.MajorId, TeacherId = x.TeacherId!.Value }));

                var majorTeachers = new Dictionary<(Guid TenantId, Guid MajorId), HashSet<Guid>>();
                foreach (var row in primaryRows.Concat(secondaryRows))
                {
                    var key = (row.TenantId!.Value, row.MajorId!.Value);
                    if (!majorTeachers.TryGetValue(key, out var set))
                    {
                        set = new HashSet<Guid>();
                        majorTeachers[key] = set;
                    }

                    set.Add(row.TeacherId);
                }

                foreach (var library in libraries.Where(x => x.PrimaryMajorId.HasValue))
                {
                    var key = (library.OwnerTenantId, library.PrimaryMajorId!.Value);
                    batch.MajorTeacherIds[library.Id] = majorTeachers.TryGetValue(key, out var set)
                        ? set
                        : new HashSet<Guid>();
                }
            }
        }

        // ⑦ 专业名
        var primaryMajorIds = libraries
            .Where(x => x.PrimaryMajorId.HasValue)
            .Select(x => x.PrimaryMajorId!.Value)
            .Distinct()
            .ToList();
        if (primaryMajorIds.Count > 0)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var majors = await AsyncExecuter.ToListAsync(
                    (await _majorRepository.GetQueryableAsync())
                        .Where(x => primaryMajorIds.Contains(x.Id))
                        .Select(x => new { x.Id, x.Name }));
                foreach (var major in majors)
                {
                    batch.MajorNames[major.Id] = major.Name;
                }
            }
        }

        // ⑧ 租户显示名（学校档案名 → 租户信息名 → 租户名）
        var tenantIds = new HashSet<Guid>();
        foreach (var library in libraries)
        {
            tenantIds.Add(library.OwnerTenantId);
        }

        foreach (var libraryMembers in batch.Members.Values)
        {
            foreach (var member in libraryMembers)
            {
                tenantIds.Add(member.MemberTenantId);
            }
        }

        if (tenantIds.Count > 0)
        {
            var tenantIdList = tenantIds.ToList();

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var schools = await AsyncExecuter.ToListAsync(
                    (await _schoolRepository.GetQueryableAsync())
                        .Where(x => tenantIdList.Contains(x.TenantId))
                        .Select(x => new { x.TenantId, x.Name }));
                var tenantInfos = await AsyncExecuter.ToListAsync(
                    (await _tenantInfoRepository.GetQueryableAsync())
                        .Where(x => tenantIdList.Contains(x.TenantId))
                        .Select(x => new { x.TenantId, x.Name }));
                var tenants = (await _tenantRepository.GetListAsync())
                    .Select(x => new { x.Id, x.Name })
                    .ToList();

                foreach (var tenantId in tenantIdList)
                {
                    var schoolName = schools.FirstOrDefault(x => x.TenantId == tenantId)?.Name;
                    var tenantInfoName = tenantInfos.FirstOrDefault(x => x.TenantId == tenantId)?.Name;
                    var tenantName = tenants.FirstOrDefault(x => x.Id == tenantId)?.Name;

                    batch.TenantNames[tenantId] = !string.IsNullOrWhiteSpace(schoolName)
                        ? schoolName
                        : (!string.IsNullOrWhiteSpace(tenantInfoName) ? tenantInfoName : tenantName);
                }
            }
        }

        return batch;
    }

    private async Task<ResourceLibraryDto> ToDtoAsync(
        ResourceLibrary library,
        Guid currentTenantId,
        Guid? currentUserId,
        bool isTenantAdmin)
    {
        var batch = await BuildLibraryBatchAsync(new List<ResourceLibrary> { library }, currentUserId);
        return await ToDtoAsync(library, batch, currentTenantId, currentUserId, isTenantAdmin);
    }

    /// <summary>
    /// 批量组装 DTO：所有「每个库一次」的查询都在 <see cref="LibraryBatch"/> 里一次性取好。
    /// </summary>
    private async Task<ResourceLibraryDto> ToDtoAsync(
        ResourceLibrary library,
        LibraryBatch batch,
        Guid currentTenantId,
        Guid? currentUserId,
        bool isTenantAdmin)
    {
        var members = batch.Members.TryGetValue(library.Id, out var libraryMembers)
            ? libraryMembers
            : new List<ResourceLibraryMember>();

        var majorName = library.PrimaryMajorId.HasValue
            ? (batch.MajorNames.TryGetValue(library.PrimaryMajorId.Value, out var name) ? name : null)
            : null;

        // 「已分配教师」口径：显式分配 ∪ 该库主建专业下的全部教师
        // （专业资源库默认由该专业的全体教师负责，不需要逐个分配）
        var teacherCount = batch.CountTeachers(library);

        // 同步内存中的共建成员：更新可见性 / 关联学校后，实体上的集合可能已过期，
        // 而 CanView / CanContribute 的判定依赖它。
        library.Members.Clear();
        foreach (var member in members)
        {
            library.Members.Add(member);
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var currentMember = members.FirstOrDefault(x => x.MemberTenantId == currentTenantId);

            var isAssigned = currentUserId.HasValue && batch.UserAssignedLibraries.Contains(library.Id);

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
                OwnerTenantName = batch.ResolveTenantName(library.OwnerTenantId, null),
                Principal = library.Principal,
                Status = library.Status,
                Visibility = library.Visibility,
                IsPublic = library.IsPublic,
                OpenToExternalLearners = library.OpenToExternalLearners,
                FormName = library.Visibility.GetFormName(),
                SharingScope = library.Visibility.GetSharingScope(),
                SortOrder = library.SortOrder,
                ResourceCount = batch.GetCount(batch.ResourceCounts, library.Id),
                CourseCount = batch.GetCount(batch.CourseCounts, library.Id),
                PracticumCount = batch.GetCount(batch.PracticumCounts, library.Id),
                MemberCount = members.Count,
                TeacherCount = teacherCount,
                CurrentRole = currentMember != null ? (int)currentMember.Role : -1,
                IsOwner = library.OwnerTenantId == currentTenantId,
                IsAssigned = isAssigned,
                CanView = await _accessService.CanViewAsync(library, currentTenantId, currentUserId, isTenantAdmin),
                CanContribute = await _accessService.CanContributeAsync(library, currentTenantId, currentUserId, isTenantAdmin),
                Members = ResolveMemberNamesAsync(members, batch)
            };
        }
    }

    #endregion
}
