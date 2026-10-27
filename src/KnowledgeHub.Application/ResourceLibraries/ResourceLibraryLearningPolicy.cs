using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>学员进入课程学习的判定结果。</summary>
public enum LibraryLearningAccess
{
    /// <summary>允许学习。</summary>
    Allowed = 0,

    /// <summary>需要先加入班级 / 由课程老师导入后才能学习（校级私有库）。</summary>
    RequireClassEnrollment = 1,

    /// <summary>需要先完成「手机号验证 + 实名认证」并登记为其他学员。</summary>
    RequireOtherLearner = 2,

    /// <summary>禁止学习。</summary>
    Denied = 3
}

/// <summary>学习判定的结果（附带原因文案，供前端直接展示）。</summary>
public class LibraryLearningDecision
{
    public LibraryLearningAccess Access { get; set; } = LibraryLearningAccess.Allowed;

    /// <summary>提示文案（Access != Allowed 时有值）。</summary>
    public string? Reason { get; set; }

    /// <summary>课程所属资源库（未挂库时为 null）。</summary>
    public ResourceLibrary? Library { get; set; }

    /// <summary>跨校共享范围。</summary>
    public ResourceLibrarySharingScope SharingScope { get; set; } = ResourceLibrarySharingScope.Unrestricted;

    public bool IsAllowed => Access == LibraryLearningAccess.Allowed;
}

/// <summary>
/// 教学资源库的「学习入口」与「跨校共享」策略 —— PRD 第 4 章的集中实现。
///
/// <para>
/// PRD 的三种形态与平台枚举的对应：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>校级私有库</b>（<see cref="ResourceLibraryVisibility.Private"/>）：
/// 前台所有人可见可浏览，但<b>仅建设老师导入班级的学员</b>可进入学习；
/// 不允许参建单位；不对外开放自学。
/// </description></item>
/// <item><description>
/// <b>校级共享库</b>（<see cref="ResourceLibraryVisibility.Public"/>）：
/// 对外开放学习（其他学员需手机号 + 实名认证），数据归入主建老师「其他学员」目录；
/// <b>不允许参建单位</b>，库内课程 / 资源<b>仅限校内共享</b>。
/// </description></item>
/// <item><description>
/// <b>校际共建库</b>（<see cref="ResourceLibraryVisibility.CoBuilt"/>）：
/// 主建单位 + 参建单位；库内课程 / 资源可共享给<b>参建单位</b>，不可共享给其它学校。
/// </description></item>
/// </list>
/// </summary>
public interface IResourceLibraryLearningPolicy
{
    /// <summary>取课程所属的资源库（跨租户，未挂库返回 null）。</summary>
    Task<ResourceLibrary?> FindCourseLibraryAsync(Guid courseId);

    /// <summary>
    /// 判定某学员能否学习某课程。
    ///
    /// <para>PRD 第 4 章的学员侧规则：</para>
    /// <list type="bullet">
    /// <item><description>本校学生 → 共享库 / 共建库课程可直接学习；私有库课程仅老师导入的学员可学。</description></item>
    /// <item><description>
    /// <b>学校之外的学生</b>（含社会学校注册用户）→ 私有库课程一律不能学；
    /// 共享库 / 共建库课程在<b>完成实名认证</b>后即可学习（PRD 4.2 / 4.3）。
    /// </description></item>
    /// </list>
    /// </summary>
    Task<LibraryLearningDecision> EvaluateLearningAsync(
        Guid courseId,
        Guid? learnerTenantId,
        Guid learnerUserId,
        bool hasExistingEnrollment);

    /// <summary>
    /// 校验「把课程共享给目标学校」是否被允许。
    /// 未挂库的课程不受限；校级私有库 / 校级共享库一律禁止跨校；校际共建库仅允许参建单位。
    /// </summary>
    Task EnsureShareToTenantAllowedAsync(Guid courseId, Guid targetTenantId);

    /// <summary>
    /// 校验「发布为共享课（共享给平台社会学校，使所有注册用户可学）」是否被允许。
    /// 校级私有库禁止；共享库 / 共建库需显式开启「对外开放自学」。
    /// </summary>
    Task EnsurePublishAsSharedCourseAllowedAsync(Guid courseId);

    /// <summary>
    /// 账号级实名认证状态（个人账户完成实名认证后为 true）。
    /// 证书申请资格、对外学习闸门共用这一口径。
    /// </summary>
    Task<bool> IsLearnerVerifiedAsync(Guid userId);

    /// <summary>
    /// 「对外开放学习」且门户公开展示的资源库 Id 集合（跨租户）：
    /// 校级共享库（Visibility=Public）+ 校际共建库（Visibility=CoBuilt），均需
    /// Status=Active 且 IsPublic=true。
    ///
    /// <para>
    /// 该集合是「校外学员可见 / 可学课程」的统一口径：课程列表、学习看板、
    /// 跨租户选课闸门都以此为准，避免各处口径漂移。
    /// </para>
    /// </summary>
    Task<List<Guid>> GetOpenToExternalLibraryIdsAsync();

    /// <summary>取课程当前的跨校共享范围（供前端做 UI 约束）。</summary>
    Task<ResourceLibrarySharingScope> GetSharingScopeAsync(Guid courseId);

    /// <summary>取课程当前的跨校共享范围说明（前端提示文案）。</summary>
    Task<string?> GetSharingScopeReasonAsync(Guid courseId);
}

public class ResourceLibraryLearningPolicy : IResourceLibraryLearningPolicy, ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<ResourceLibraryLearner, Guid> _learnerRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ResourceLibraryLearningPolicy(
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IResourceLibraryRepository libraryRepository,
        IRepository<ResourceLibraryLearner, Guid> learnerRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _libraryRepository = libraryRepository;
        _learnerRepository = learnerRepository;
        _identityUserRepository = identityUserRepository;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<ResourceLibrary?> FindCourseLibraryAsync(Guid courseId)
    {
        Guid? libraryId;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var course = await _courseRepository.FirstOrDefaultAsync(x => x.Id == courseId);
            libraryId = course?.LibraryId;
        }

        if (!libraryId.HasValue || libraryId.Value == Guid.Empty)
        {
            return null;
        }

        var library = await _libraryRepository.FindWithMembersAsync(libraryId.Value);
        if (library == null)
        {
            return null;
        }

        return library;
    }

    public async Task<LibraryLearningDecision> EvaluateLearningAsync(
        Guid courseId,
        Guid? learnerTenantId,
        Guid learnerUserId,
        bool hasExistingEnrollment)
    {
        var library = await FindCourseLibraryAsync(courseId);
        if (library == null)
        {
            return new LibraryLearningDecision
            {
                Access = LibraryLearningAccess.Allowed,
                SharingScope = ResourceLibrarySharingScope.Unrestricted
            };
        }

        var scope = library.Visibility.GetSharingScope();
        var isOwnerSchool = learnerTenantId.HasValue && learnerTenantId.Value == library.OwnerTenantId;
        var isPartnerSchool = learnerTenantId.HasValue &&
                              library.Members.Any(m => m.MemberTenantId == learnerTenantId.Value);

        // 校际共建库：主建校 + 参建单位的学员可正常学习
        if (library.Visibility == ResourceLibraryVisibility.CoBuilt && (isOwnerSchool || isPartnerSchool))
        {
            return new LibraryLearningDecision
            {
                Access = LibraryLearningAccess.Allowed,
                Library = library,
                SharingScope = scope
            };
        }

        // 校级私有库：仅建设老师导入（已存在选课记录）的学员可学
        if (library.Visibility == ResourceLibraryVisibility.Private)
        {
            if (isOwnerSchool)
            {
                return hasExistingEnrollment
                    ? new LibraryLearningDecision
                    {
                        Access = LibraryLearningAccess.Allowed,
                        Library = library,
                        SharingScope = scope
                    }
                    : new LibraryLearningDecision
                    {
                        Access = LibraryLearningAccess.RequireClassEnrollment,
                        Reason = $"「{library.Name}」为校级私有教学资源库，仅课程老师导入班级的学员可以学习，请联系课程老师加入班级。",
                        Library = library,
                        SharingScope = scope
                    };
            }

            return new LibraryLearningDecision
            {
                Access = LibraryLearningAccess.Denied,
                Reason = $"「{library.Name}」为校级私有教学资源库，仅限本校课程老师导入的学员学习。",
                Library = library,
                SharingScope = scope
            };
        }

        // 校级共享库（或未开放自学的共建库）：本校学员直接可学，外校学员需登记为「其他学员」
        if (isOwnerSchool || isPartnerSchool)
        {
            return new LibraryLearningDecision
            {
                Access = LibraryLearningAccess.Allowed,
                Library = library,
                SharingScope = scope
            };
        }

        if (!library.OpenToExternalLearners)
        {
            return new LibraryLearningDecision
            {
                Access = LibraryLearningAccess.Denied,
                Reason = $"「{library.Name}」仅限校内学习，暂未对外开放。",
                Library = library,
                SharingScope = scope
            };
        }

        // 学校之外的学生：需完成账号级实名认证后方可学习（手机号认证 + 实名认证）
        if (!await IsLearnerVerifiedAsync(learnerUserId))
        {
            return new LibraryLearningDecision
            {
                Access = LibraryLearningAccess.RequireOtherLearner,
                Reason = $"「{library.Name}」对外开放学习，请先在个人账户完成实名认证后再学习。",
                Library = library,
                SharingScope = scope
            };
        }

        return new LibraryLearningDecision
        {
            Access = LibraryLearningAccess.Allowed,
            Library = library,
            SharingScope = scope
        };
    }

    /// <summary>账号级实名认证状态（IdentityUser 扩展属性）。</summary>
    public async Task<bool> IsLearnerVerifiedAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var user = await _identityUserRepository.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                return false;
            }

            return user.GetProperty<bool>("IsRealNameVerified");
        }
    }

    /// <summary>取学员在该库的「其他学员」档案（跨租户：学员可能属于社会学校租户）。</summary>
    private async Task<ResourceLibraryLearner?> FindLearnerAsync(Guid libraryId, Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _learnerRepository.FirstOrDefaultAsync(x => x.LibraryId == libraryId && x.UserId == userId);
        }
    }

    public async Task EnsureShareToTenantAllowedAsync(Guid courseId, Guid targetTenantId)
    {
        var library = await FindCourseLibraryAsync(courseId);
        if (library == null)
        {
            return;
        }

        var scope = library.Visibility.GetSharingScope();
        if (scope == ResourceLibrarySharingScope.SchoolOnly)
        {
            throw new UserFriendlyException(
                $"「{library.Name}」是{library.Visibility.GetFormName()}，按需求库内课程仅限校内共享，不能共享给其他学校。");
        }

        if (scope == ResourceLibrarySharingScope.CoBuildMembersOnly)
        {
            var isMember = library.Members.Any(m => m.MemberTenantId == targetTenantId);
            if (!isMember)
            {
                throw new UserFriendlyException(
                    $"「{library.Name}」是校际共建库，课程只能共享给参建单位，不能共享给其它学校。");
            }
        }
    }

    public async Task EnsurePublishAsSharedCourseAllowedAsync(Guid courseId)
    {
        var library = await FindCourseLibraryAsync(courseId);
        if (library == null)
        {
            return;
        }

        if (!library.OpenToExternalLearners)
        {
            throw new UserFriendlyException(
                $"「{library.Name}」是{library.Visibility.GetFormName()}，不对外开放自学，不能发布为共享课。");
        }
    }

    public async Task<List<Guid>> GetOpenToExternalLibraryIdsAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var libraryQuery = await _libraryRepository.GetQueryableAsync();
            return await _asyncExecuter.ToListAsync(
                libraryQuery
                    .Where(l => (l.Visibility == ResourceLibraryVisibility.Public ||
                                 l.Visibility == ResourceLibraryVisibility.CoBuilt) &&
                                l.Status == ResourceLibraryStatus.Active &&
                                l.IsPublic)
                    .Select(l => l.Id));
        }
    }

    public async Task<ResourceLibrarySharingScope> GetSharingScopeAsync(Guid courseId)
    {
        var library = await FindCourseLibraryAsync(courseId);
        return library?.Visibility.GetSharingScope() ?? ResourceLibrarySharingScope.Unrestricted;
    }

    public async Task<string?> GetSharingScopeReasonAsync(Guid courseId)
    {
        var library = await FindCourseLibraryAsync(courseId);
        if (library == null)
        {
            return null;
        }

        return library.Visibility.GetSharingScope() switch
        {
            ResourceLibrarySharingScope.SchoolOnly =>
                $"该课程属于「{library.Name}」（{library.Visibility.GetFormName()}），按需求仅限校内共享，不能共享给其它学校。",
            ResourceLibrarySharingScope.CoBuildMembersOnly =>
                $"该课程属于「{library.Name}」（校际共建库），只能共享给参建单位。",
            _ => null
        };
    }
}
