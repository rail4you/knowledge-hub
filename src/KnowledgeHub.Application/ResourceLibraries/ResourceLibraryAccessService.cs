using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库可见性 / 可写性判定。
///
/// <para>
/// 这是「租户 = 学校」下教学资源库模型的核心规则实现，管理端与教师端两个
/// AppService 共用同一套判定，避免两侧逻辑漂移。
/// </para>
///
/// <para>
/// 判定规则（<see cref="ResourceLibraryVisibility"/>）：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>私有</b>：<b>被分配教师</b>可见可写；此外「本专业教师」也可看到/写入本专业的私有专业资源库
/// （教师专业 = 其所授课程的专业，含兼属专业）。本校院校管理员始终可见可写。
/// </description></item>
/// <item><description>
/// <b>公开</b>：仅<b>被分配到该库</b>的教师可见可写（不再默认对全校教师开放）；
/// 不含关联学校。
/// </description></item>
/// <item><description>
/// <b>院校共建</b>：仅<b>被分配到该库</b>的主建校 / 关联校教师可见可写，
/// 可互相看到对方共享进来的内容。
/// </description></item>
/// </list>
///
/// <para>
/// 院校管理员（角色 <c>SchoolAdmin</c>，以及 host 的 <c>admin</c>）对
/// 本租户名下（含作为关联方参与）的全部库拥有管理权，不受可见性限制。
/// </para>
/// </summary>
public interface IResourceLibraryAccessService
{
    /// <summary>取当前用户可见的教学资源库（含主建的 + 参与共建的）。</summary>
    Task<List<ResourceLibrary>> GetVisibleLibrariesAsync(
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>取库并校验可见性；不可见时抛业务异常。</summary>
    Task<ResourceLibrary> GetLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>取库并校验可写性（可向库内共享内容）；不可写时抛业务异常。</summary>
    Task<ResourceLibrary> GetContributableLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>判断当前用户是否能看到该库。</summary>
    Task<bool> CanViewAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>判断当前用户是否可以向该库共享内容。</summary>
    Task<bool> CanContributeAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>教师是否被分配到该库。</summary>
    Task<bool> IsAssignedTeacherAsync(Guid libraryId, Guid userId);

    /// <summary>包含「我创建的课程」的资源库 Id 集合（主库字段 ∪ 关联表）。</summary>
    Task<HashSet<Guid>> GetMyCourseLibraryIdsAsync(Guid userId);

    /// <summary>我作为「库的关联老师」的资源库 Id 集合。</summary>
    Task<HashSet<Guid>> GetLibraryIdsWhereIAmTeacherAsync(Guid userId);

    /// <summary>把当前用户登记为该库的关联老师（幂等；共享内容进库时调用）。</summary>
    Task EnsureLibraryTeacherAsync(Guid libraryId, Guid userId, string? userName, Guid? tenantId);

    /// <summary>
    /// 教师「自己的库」范围：**只认「库的关联老师」记录**（`AppResourceLibraryTeachers`）。
    ///
    /// <para>
    /// 工作台 / 资源互转 / 资源中心必须用同一套口径。
    /// 仅因「库里含我的课程 / 我的资源」不视为成员（可能是脏数据或他人代挂）；
    /// 共享内容进库时会自动把我登记为关联老师（见 EnsureLibraryTeacherAsync）。
    /// 院校管理员不缩口（返回本租户全部候选库）。
    /// </para>
    /// </summary>
    Task<HashSet<Guid>> GetMyLibraryIdsAsync(Guid tenantId, Guid? userId, bool isTenantAdmin);

    /// <summary>取可见性中文说明。</summary>
    string GetVisibilityName(ResourceLibraryVisibility visibility);
}

public class ResourceLibraryAccessService : IResourceLibraryAccessService, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;
    /// <summary>课程仓储：教师「所在专业」以其所授课程的专业推导。</summary>
    private readonly Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.Courses.Course, Guid> _courseRepository;
    /// <summary>课程—专业关联表（含兼属专业）。</summary>
    private readonly Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.Courses.CourseMajor, Guid> _courseMajorRepository;
    /// <summary>企业成员表：用于识别「企业账号」，它不占教师编制但可向本校库贡献内容。</summary>
    private readonly Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.Enterprises.EnterpriseMember, Guid> _enterpriseMemberRepository;
    /// <summary>课程—资源库关联表（老师「含自己课程的库」口径）。</summary>
    private readonly Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.ResourceLibraries.ResourceLibraryCourse, Guid> _courseLibraryRepository;
    /// <summary>资源库—关联教师表（「库的关联老师」口径）。</summary>
    private readonly Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.ResourceLibraries.ResourceLibraryTeacher, Guid> _libraryTeacherRepository;
    private readonly Volo.Abp.Data.IDataFilter _dataFilter;
    private readonly Volo.Abp.Linq.IAsyncQueryableExecuter _asyncExecuter;

    public ResourceLibraryAccessService(
        IResourceLibraryRepository libraryRepository,
        Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.Courses.Course, Guid> courseRepository,
        Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.Courses.CourseMajor, Guid> courseMajorRepository,
        Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.Enterprises.EnterpriseMember, Guid> enterpriseMemberRepository,
        Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.ResourceLibraries.ResourceLibraryCourse, Guid> courseLibraryRepository,
        Volo.Abp.Domain.Repositories.IRepository<KnowledgeHub.ResourceLibraries.ResourceLibraryTeacher, Guid> libraryTeacherRepository,
        Volo.Abp.Data.IDataFilter dataFilter,
        Volo.Abp.Linq.IAsyncQueryableExecuter asyncExecuter)
    {
        _libraryRepository = libraryRepository;
        _courseRepository = courseRepository;
        _courseMajorRepository = courseMajorRepository;
        _enterpriseMemberRepository = enterpriseMemberRepository;
        _courseLibraryRepository = courseLibraryRepository;
        _libraryTeacherRepository = libraryTeacherRepository;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetMyLibraryIdsAsync(Guid tenantId, Guid? userId, bool isTenantAdmin)
    {
        var candidates = await _libraryRepository.GetByTenantOrMemberAsync(tenantId);

        // 院校管理员：本租户全部候选库
        if (isTenantAdmin)
        {
            return candidates.Select(x => x.Id).ToHashSet();
        }

        // 严格口径：**必须是我在该库有「关联老师」记录**（AppResourceLibraryTeachers）。
        // 仅因「库里含我的课程 / 我的资源」不算——那可能是脏数据或他人代挂，不能据此把我算成该库成员。
        if (!userId.HasValue)
        {
            return new HashSet<Guid>();
        }

        return await GetLibraryIdsWhereIAmTeacherAsync(userId.Value);
    }

    /// <summary>我作为「库的关联老师」的资源库 Id 集合。</summary>
    public async Task<HashSet<Guid>> GetLibraryIdsWhereIAmTeacherAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _libraryTeacherRepository.GetQueryableAsync();
            var ids = await _asyncExecuter.ToListAsync(
                query.Where(x => x.UserId == userId).Select(x => x.LibraryId));
            return ids.ToHashSet();
        }
    }

    /// <summary>
    /// 确保当前用户是该库的关联老师（共享内容进库后自动关联，幂等）。
    /// 这样「我的库」口径（关联老师）与「我贡献过内容」始终一致。
    /// </summary>
    public async Task EnsureLibraryTeacherAsync(Guid libraryId, Guid userId, string? userName, Guid? tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _libraryTeacherRepository.GetQueryableAsync();
            var exists = await _asyncExecuter.AnyAsync(
                query.Where(x => x.LibraryId == libraryId && x.UserId == userId));
            if (exists)
            {
                return;
            }

            await _libraryTeacherRepository.InsertAsync(
                new KnowledgeHub.ResourceLibraries.ResourceLibraryTeacher(
                    Guid.NewGuid(), libraryId, userId, userName, tenantId),
                autoSave: true);
        }
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetMyCourseLibraryIdsAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var courseQuery = await _courseRepository.GetQueryableAsync();
            var myCourses = courseQuery.Where(x => x.TeacherId == userId || x.CreatorId == userId);

            var libraryIds = await _asyncExecuter.ToListAsync(
                myCourses.Where(x => x.LibraryId.HasValue).Select(x => x.LibraryId!.Value));

            var myCourseIds = await _asyncExecuter.ToListAsync(myCourses.Select(x => x.Id));
            if (myCourseIds.Count > 0)
            {
                var linkQuery = await _courseLibraryRepository.GetQueryableAsync();
                libraryIds.AddRange(await _asyncExecuter.ToListAsync(
                    linkQuery.Where(x => myCourseIds.Contains(x.CourseId)).Select(x => x.LibraryId)));
            }

            return libraryIds.Where(x => x != Guid.Empty).ToHashSet();
        }
    }

    public async Task<List<ResourceLibrary>> GetVisibleLibrariesAsync(
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        // 候选集必须跨租户查询：共建库的归属租户可能是别的学校
        var candidates = await _libraryRepository.GetByTenantOrMemberAsync(tenantId);

        if (isTenantAdmin)
        {
            return candidates;
        }

        var visible = new List<ResourceLibrary>();
        foreach (var library in candidates)
        {
            if (await CanViewCoreAsync(library, tenantId, userId))
            {
                visible.Add(library);
            }
        }

        return visible;
    }

    public async Task<ResourceLibrary> GetLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        var library = await FindLibraryAsync(libraryId);
        if (!await CanViewAsync(library, tenantId, userId, isTenantAdmin))
        {
            throw new UserFriendlyException("教学资源库不存在或无权访问。");
        }

        return library;
    }

    public async Task<ResourceLibrary> GetContributableLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        var library = await FindLibraryAsync(libraryId);
        if (!await CanContributeAsync(library, tenantId, userId, isTenantAdmin))
        {
            throw new UserFriendlyException("无权向该教学资源库共享内容。");
        }

        return library;
    }

    public async Task<bool> CanViewAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        if (isTenantAdmin)
        {
            // 院校管理员对本租户名下（含作为关联方参与）的库一律可见
            return library.OwnerTenantId == tenantId ||
                   library.Members.Any(m => m.MemberTenantId == tenantId);
        }

        return await CanViewCoreAsync(library, tenantId, userId);
    }

    public async Task<bool> CanContributeAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        // 未发布（审核中 / 已驳回 / 已归档）的库不接受新内容：避免「审核中的库还在长内容」。
        if (library.Status != ResourceLibraryStatus.Active)
        {
            return false;
        }

        var isOwner = library.OwnerTenantId == tenantId;
        var isPartner = library.Members.Any(m => m.MemberTenantId == tenantId);

        if (isTenantAdmin)
        {
            return isOwner || isPartner;
        }

        // 企业账号（企业成员）：可向「本校主建」的库贡献内容。
        // 企业不占教师编制，不能靠「被分配教师」进库，但必须能看见库并提交内容
        // （V2 前端验收 P0-3：企业工作台看不到任何库 → 无法贡献）。
        // 关联校（共建方）的库对企业只读，不允许贡献（与旧兜底规则一致）。
        if (userId.HasValue && await IsEnterpriseUserAsync(userId.Value))
        {
            return isOwner;
        }

        switch (library.Visibility)
        {
            case ResourceLibraryVisibility.Private:
                // 私有专业库：被分配教师 + 本专业教师可写
                if (!isOwner)
                {
                    return false;
                }

                if (userId.HasValue && await IsAssignedTeacherAsync(library.Id, userId.Value))
                {
                    return true;
                }

                return await IsTeacherOfLibraryMajorAsync(library, userId);

            case ResourceLibraryVisibility.Public:
                // 公开库：被分配教师 / 主建专业默认教师可写（院校管理员不受限）
                return isOwner && userId.HasValue && await IsLibraryTeacherAsync(library, userId.Value, isOwner);

            case ResourceLibraryVisibility.CoBuilt:
                // 共建库：主建校的「被分配 / 主建专业默认教师」+ 关联校的被分配教师可写
                if (!isOwner && !isPartner)
                {
                    return false;
                }

                return userId.HasValue && await IsLibraryTeacherAsync(library, userId.Value, isOwner);

            default:
                return false;
        }
    }

    /// <summary>
    /// 是否算「库内教师」：显式分配的教学教师，
    /// 或（主建校侧）主建专业的默认教师 —— 与「已分配教师 N 人」的统计口径一致。
    /// </summary>
    private async Task<bool> IsLibraryTeacherAsync(ResourceLibrary library, Guid userId, bool isOwner)
    {
        if (await IsAssignedTeacherAsync(library.Id, userId))
        {
            return true;
        }

        // 默认专业教师只对库归属学校生效（关联校教师仍需显式分配）
        return isOwner && await IsTeacherOfLibraryMajorAsync(library, userId);
    }

    public async Task<bool> IsAssignedTeacherAsync(Guid libraryId, Guid userId)
    {
        return await _libraryRepository.IsTeacherAssignedAsync(libraryId, userId);
    }

    /// <summary>
    /// 当前用户是否为企业账号（企业成员）。
    /// 企业成员表的 <c>TenantId</c> = 入驻学校租户，与当前租户一致时才算本校企业账号。
    /// </summary>
    private async Task<bool> IsEnterpriseUserAsync(Guid userId)
    {
        var query = await _enterpriseMemberRepository.GetQueryableAsync();
        return await _asyncExecuter.AnyAsync(query.Where(x => x.UserId == userId));
    }

    public string GetVisibilityName(ResourceLibraryVisibility visibility)
    {
        return visibility switch
        {
            ResourceLibraryVisibility.Private => "私有",
            ResourceLibraryVisibility.Public => "公开",
            ResourceLibraryVisibility.CoBuilt => "院校共建",
            _ => "未知"
        };
    }

    /// <summary>
    /// 非管理员的可见性核心判定。
    /// </summary>
    private async Task<bool> CanViewCoreAsync(ResourceLibrary library, Guid tenantId, Guid? userId)
    {
        // V2.0 审核态门禁：非 Active 的库只对「管理端」（院校管理员）可见。
        // 待审 / 变更中 / 待删除 / 已驳回 / 已归档的库不得对成员校教师与学生暴露，
        // 否则会出现「审核还没过，学生已经能看到库」的越权。
        if (library.Status != ResourceLibraryStatus.Active)
        {
            return false;
        }

        var isOwner = library.OwnerTenantId == tenantId;
        var isPartner = library.Members.Any(m => m.MemberTenantId == tenantId);

        // 企业账号（企业成员）：可见本校（主责校 / 关联校）的库，
        // 以便在「教学资源库工作台」向库贡献内容（V2 前端验收 P0-3）。
        if (userId.HasValue && await IsEnterpriseUserAsync(userId.Value))
        {
            return isOwner || isPartner;
        }

        switch (library.Visibility)
        {
            case ResourceLibraryVisibility.Private:
                // 私有专业资源库：被分配教师 + 「本专业教师」（专业 = 其授课专业）可见
                if (!isOwner)
                {
                    return false;
                }

                if (userId.HasValue && await IsAssignedTeacherAsync(library.Id, userId.Value))
                {
                    return true;
                }

                return await IsTeacherOfLibraryMajorAsync(library, userId);

            case ResourceLibraryVisibility.Public:
                // 公开库：被分配教师 或 主建专业的默认教师可见（院校管理员见全部）
                return isOwner && userId.HasValue && await IsLibraryTeacherAsync(library, userId.Value, isOwner);

            case ResourceLibraryVisibility.CoBuilt:
                // 共建库：主建校的「被分配教师 / 主建专业默认教师」+ 关联校的被分配教师
                if (!isOwner && !isPartner)
                {
                    return false;
                }

                return userId.HasValue && await IsLibraryTeacherAsync(library, userId.Value, isOwner);

            default:
                return false;
        }
    }

    /// <summary>
    /// 教师是否属于该库的主建专业：
    /// 库有主建专业时，教师所授课程（含兼属专业）命中该专业即可见。
    /// </summary>
    private async Task<bool> IsTeacherOfLibraryMajorAsync(ResourceLibrary library, Guid? userId)
    {
        if (!library.PrimaryMajorId.HasValue || !userId.HasValue)
        {
            return false;
        }

        var majorId = library.PrimaryMajorId.Value;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var courseQuery = await _courseRepository.GetQueryableAsync();
            var majorLinkQuery = await _courseMajorRepository.GetQueryableAsync();

            // 与「已分配教师人数」统计同一口径：主专业字段（Course.MajorId）∪ 兼属专业（CourseMajor）
            return await _asyncExecuter.AnyAsync(
                courseQuery.Where(x => x.TeacherId == userId.Value &&
                                       (x.MajorId == majorId ||
                                        majorLinkQuery.Any(m => m.CourseId == x.Id && m.MajorId == majorId))));
        }
    }

    /// <summary>跨租户取库（库的归属租户可能是别校），并预加载共建成员。</summary>
    private async Task<ResourceLibrary> FindLibraryAsync(Guid libraryId)
    {
        var library = await _libraryRepository.FindWithMembersAsync(libraryId);
        if (library == null)
        {
            throw new UserFriendlyException("教学资源库不存在。");
        }

        return library;
    }
}
