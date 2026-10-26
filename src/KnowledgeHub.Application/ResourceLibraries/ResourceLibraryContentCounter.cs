using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Data;
using Volo.Abp.Linq;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库的内容数量统计（课程数 / 资源数）。
///
/// <para>
/// 统计口径（与门户「公开资源库详情」一致，避免不同页面数字对不上）：
/// </para>
/// <list type="bullet">
/// <item><description>
/// 库内课程 = ①显式挂进本库（主库字段 <c>Course.LibraryId</c> / 课程—库关联表）
/// ②库设了主建专业时，主建学校该专业下的**已发布**课程（新建库尚未逐条挂载时的兜底）
/// </description></item>
/// <item><description>
/// 库内资源 = 同上，状态取「学院审核通过及以上」
/// </description></item>
/// </list>
///
/// <para>
/// 注意：兜底**只在库设了主建专业时生效**；没有主建专业的库（公共库 / 历史共享库）
/// 只统计显式挂载的内容，否则会把本校全部已发布课程都算进来。
/// </para>
/// </summary>
public interface IResourceLibraryContentCounter
{
    /// <summary>统计指定资源库的库内课程数 / 资源数。</summary>
    Task<(int CourseCount, int ResourceCount)> CountAsync(ResourceLibrary library);
}

public class ResourceLibraryContentCounter : IResourceLibraryContentCounter, ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceLibraryCourse, Guid> _courseLinkRepository;
    private readonly IRepository<ResourceLibraryResource, Guid> _resourceLinkRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ResourceLibraryContentCounter(
        IRepository<Course, Guid> courseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceLibraryCourse, Guid> courseLinkRepository,
        IRepository<ResourceLibraryResource, Guid> resourceLinkRepository,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _courseLinkRepository = courseLinkRepository;
        _resourceLinkRepository = resourceLinkRepository;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<(int CourseCount, int ResourceCount)> CountAsync(ResourceLibrary library)
    {
        var libraryId = library.Id;
        var ownerTenantId = library.OwnerTenantId;
        var primaryMajorId = library.PrimaryMajorId;

        // 库可能属于别校，查询必须跨租户
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var courseLinkQuery = await _courseLinkRepository.GetQueryableAsync();
            var linkedCourseIds = await _asyncExecuter.ToListAsync(
                courseLinkQuery.Where(l => l.LibraryId == libraryId).Select(l => l.CourseId));

            var courseCount = await _asyncExecuter.CountAsync(
                (await _courseRepository.GetQueryableAsync())
                    .Where(x => x.Status == CourseStatus.Published)
                    .Where(x => x.LibraryId == libraryId
                                || linkedCourseIds.Contains(x.Id)
                                || (primaryMajorId.HasValue
                                    && x.TenantId == ownerTenantId
                                    && x.MajorId == primaryMajorId.Value)));

            var resourceLinkQuery = await _resourceLinkRepository.GetQueryableAsync();
            var linkedResourceIds = await _asyncExecuter.ToListAsync(
                resourceLinkQuery.Where(l => l.LibraryId == libraryId).Select(l => l.ResourceId));

            var resourceCount = await _asyncExecuter.CountAsync(
                (await _resourceRepository.GetQueryableAsync())
                    .Where(x => x.Status >= ResourceStatus.SchoolApproved)
                    .Where(x => x.LibraryId == libraryId
                                || linkedResourceIds.Contains(x.Id)
                                || (primaryMajorId.HasValue
                                    && x.TenantId == ownerTenantId
                                    && x.MajorId == primaryMajorId.Value)));

            return (courseCount, resourceCount);
        }
    }
}
