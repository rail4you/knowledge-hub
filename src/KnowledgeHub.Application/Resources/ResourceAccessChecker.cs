using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Resources;

/// <summary>资源访问意图（不同操作的能力要求不同）。</summary>
public enum ResourceAccessIntent
{
    /// <summary>预览（不下载）。</summary>
    Preview = 0,

    /// <summary>下载（还需 IsDownloadable 与下载权限）。</summary>
    Download = 1,

    /// <summary>学习（学生路径：需课程/引用关系）。</summary>
    Study = 2,

    /// <summary>进入 AI 问答的检索语料（不得放宽可见性）。</summary>
    AiIndex = 3,

    /// <summary>出现在检索结果里。</summary>
    SearchHit = 4
}

/// <summary>资源访问判定结果。</summary>
public class ResourceAccessResult
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// 统一的资源访问判定（V2.0 P6）。
///
/// <para>
/// 之前 AI 问答、检索、文件流各自实现了一套「能不能看」的规则：
/// AI 只按「本租户 + 已审核」取资源（会漏掉共建库的别校资源，也不校验私有库分配），
/// 检索只按 tenantId 过滤。本类把判定收敛到一处，供三处共用。
/// </para>
///
/// <para><b>判定链</b>（全部通过才允许）：</para>
/// <list type="number">
/// <item><description>资源状态必须是 SchoolApproved / LeagueApproved（草稿、待审、驳回一律不可见）。</description></item>
/// <item><description>本校资源：若挂在<b>私有库</b>，需要被分配教师或院校管理员；否则放行。</description></item>
/// <item><description>他校资源：必须是本校参与的<b>院校共建库</b>（库 Active）里已发布的内容。</description></item>
/// <item><description>下载意图：额外要求资源可下载（<c>IsDownloadable</c>）且持有下载权限。</description></item>
/// </list>
/// </summary>
public interface IResourceAccessChecker
{
    /// <summary>单个资源判定。</summary>
    Task<ResourceAccessResult> CanAccessAsync(Guid resourceId, ResourceAccessIntent intent);

    /// <summary>批量过滤（保留可访问的 Id），用于 AI 语料与检索结果的收口。</summary>
    Task<HashSet<Guid>> FilterAccessibleAsync(
        IEnumerable<Guid> resourceIds,
        ResourceAccessIntent intent,
        bool includeCrossTenantLibraryContent = true);
}

public class ResourceAccessChecker : IResourceAccessChecker, ITransientDependency
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourcePublication, Guid> _publicationRepository;
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly IRepository<ResourceLibraryTeacher, Guid> _teacherRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ResourceAccessChecker(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourcePublication, Guid> publicationRepository,
        IRepository<ResourceLibrary, Guid> libraryRepository,
        IRepository<ResourceLibraryTeacher, Guid> teacherRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _resourceRepository = resourceRepository;
        _publicationRepository = publicationRepository;
        _libraryRepository = libraryRepository;
        _teacherRepository = teacherRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<ResourceAccessResult> CanAccessAsync(Guid resourceId, ResourceAccessIntent intent)
    {
        var allowed = await FilterAccessibleAsync(new[] { resourceId }, intent);
        return allowed.Contains(resourceId)
            ? new ResourceAccessResult { Allowed = true }
            : new ResourceAccessResult { Allowed = false, Reason = "无权访问该资源" };
    }

    public async Task<HashSet<Guid>> FilterAccessibleAsync(
        IEnumerable<Guid> resourceIds,
        ResourceAccessIntent intent,
        bool includeCrossTenantLibraryContent = true)
    {
        var ids = resourceIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var tenantId = _currentTenant.Id;
        var userId = _currentUser.Id;
        var isAdmin = IsTenantAdmin();

        List<Resource> resources;
        List<ResourcePublication> publications;
        List<ResourceLibrary> libraries;
        HashSet<Guid> assignedLibraryIds;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            resources = await _asyncExecuter.ToListAsync(
                (await _resourceRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));

            // 库维度准入：内容必须已发布到库才算「共享出去」
            publications = await _asyncExecuter.ToListAsync(
                (await _publicationRepository.GetQueryableAsync())
                    .Where(x => ids.Contains(x.ContentId)
                                && x.ContentType == LibraryContentType.Resource
                                && x.Status == ResourcePublicationStatus.Published));

            var libraryIds = publications.Select(x => x.LibraryId).Distinct().ToList();
            libraries = libraryIds.Count == 0
                ? new List<ResourceLibrary>()
                : await _asyncExecuter.ToListAsync(
                    (await _libraryRepository.GetQueryableAsync()).Where(x => libraryIds.Contains(x.Id)));

            assignedLibraryIds = userId.HasValue
                ? (await _asyncExecuter.ToListAsync(
                    (await _teacherRepository.GetQueryableAsync())
                        .Where(x => x.UserId == userId.Value)
                        .Select(x => x.LibraryId))).ToHashSet()
                : new HashSet<Guid>();
        }

        var libraryMap = libraries.ToDictionary(x => x.Id);
        var publishedByResource = publications
            .GroupBy(x => x.ContentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.LibraryId).ToList());

        var allowed = new HashSet<Guid>();

        foreach (var resource in resources)
        {
            if (resource.Status is not (ResourceStatus.SchoolApproved or ResourceStatus.LeagueApproved))
            {
                continue;
            }

            if (intent == ResourceAccessIntent.Download && !resource.IsDownloadable)
            {
                continue;
            }

            var pubLibraryIds = publishedByResource.GetValueOrDefault(resource.Id) ?? new List<Guid>();
            var isOwnerTenant = tenantId.HasValue && resource.TenantId == tenantId.Value;

            if (isOwnerTenant)
            {
                // 本校资源：只有在「私有库」里才需要分配；其余（未挂库 / 公开库 / 共建库）本校可见
                var privateLibraryBlocking = pubLibraryIds
                    .Select(id => libraryMap.GetValueOrDefault(id))
                    .Where(lib => lib != null)
                    .Any(lib => lib!.Visibility == ResourceLibraryVisibility.Private
                                && (lib.Status != ResourceLibraryStatus.Active
                                    || !assignedLibraryIds.Contains(lib.Id)));

                var inSomeLibrary = pubLibraryIds.Count > 0;
                if (isAdmin || !inSomeLibrary || !privateLibraryBlocking)
                {
                    allowed.Add(resource.Id);
                }

                continue;
            }

            // 他校资源：必须是本校参与的「院校共建库」里已发布的内容
            if (!includeCrossTenantLibraryContent || !tenantId.HasValue)
            {
                continue;
            }

            var sharedIntoMyLibrary = pubLibraryIds.Any(id =>
            {
                var lib = libraryMap.GetValueOrDefault(id);
                return lib != null
                       && lib.Visibility == ResourceLibraryVisibility.CoBuilt
                       && lib.Status == ResourceLibraryStatus.Active
                       && lib.Members.Any(m => m.MemberTenantId == tenantId.Value);
            });

            if (sharedIntoMyLibrary)
            {
                allowed.Add(resource.Id);
            }
        }

        return allowed;
    }

    private bool IsTenantAdmin()
    {
        var roles = _currentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }
}
