using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Practicums;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 课程 / 实训 与 教学资源库 的多对多关联维护（与资源的关联逻辑同构）。
///
/// <para>
/// 写入规则：
/// <list type="bullet">
/// <item><description>逐个校验可挂载性（本校主建，或参与共建的库）；库内内容不再受主建专业限制。</description></item>
/// <item><description>实训额外限制：只能挂到「已有课程」的库；若实训已关联课程，
/// 只能挂到包含该课程的库。</description></item>
/// <item><description>第一个库镜像到实体上的 <c>LibraryId</c>（主资源库），兼容既有按库归属的查询。</description></item>
/// </list>
/// </para>
/// </summary>
public interface IResourceLibraryContentLinkService
{
    /// <summary>同步课程的资源库关联，返回主资源库 Id（无关联时为 null）。</summary>
    Task<Guid?> SyncCourseLibrariesAsync(Guid courseId, IReadOnlyList<Guid>? libraryIds, Guid? majorId, Guid tenantId);

    /// <summary>同步实训项目的资源库关联，返回主资源库 Id（无关联时为 null）。</summary>
    Task<Guid?> SyncPracticumLibrariesAsync(Guid practicumId, IReadOnlyList<Guid>? libraryIds, Guid tenantId, Guid? courseId);
}

public class ResourceLibraryContentLinkService : IResourceLibraryContentLinkService, ITransientDependency
{
    private readonly IAsyncQueryableExecuter AsyncExecuter;
    private readonly Volo.Abp.Guids.IGuidGenerator GuidGenerator;
    private readonly IResourceLibraryAttachmentValidator _attachmentValidator;
    private readonly IRepository<ResourceLibraryCourse, Guid> _courseLinkRepository;
    private readonly IRepository<ResourceLibraryPracticum, Guid> _practicumLinkRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IDataFilter _dataFilter;

    public ResourceLibraryContentLinkService(
        IResourceLibraryAttachmentValidator attachmentValidator,
        IRepository<ResourceLibraryCourse, Guid> courseLinkRepository,
        IRepository<ResourceLibraryPracticum, Guid> practicumLinkRepository,
        IRepository<Course, Guid> courseRepository,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter,
        Volo.Abp.Guids.IGuidGenerator guidGenerator)
    {
        AsyncExecuter = asyncExecuter;
        GuidGenerator = guidGenerator;
        _attachmentValidator = attachmentValidator;
        _courseLinkRepository = courseLinkRepository;
        _practicumLinkRepository = practicumLinkRepository;
        _courseRepository = courseRepository;
        _dataFilter = dataFilter;
    }

    public async Task<Guid?> SyncCourseLibrariesAsync(
        Guid courseId,
        IReadOnlyList<Guid>? libraryIds,
        Guid? majorId,
        Guid tenantId)
    {
        var requested = Normalize(libraryIds);
        var validated = new List<Guid>(requested.Count);

        foreach (var libraryId in requested)
        {
            // 校验器负责「可挂载性」（本校主建 / 参与共建），不通过直接抛业务异常
            var ok = await _attachmentValidator.ValidateAsync(libraryId, tenantId);
            if (ok.HasValue && !validated.Contains(ok.Value))
            {
                validated.Add(ok.Value);
            }
        }

        var query = await _courseLinkRepository.GetQueryableAsync();
        var existing = await AsyncExecuter.ToListAsync(query.Where(x => x.CourseId == courseId));

        var toRemove = existing.Where(x => !validated.Contains(x.LibraryId)).ToList();
        if (toRemove.Count > 0)
        {
            await _courseLinkRepository.DeleteManyAsync(toRemove, autoSave: true);
        }

        var existingIds = existing.Select(x => x.LibraryId).ToList();
        var toAdd = validated
            .Where(id => !existingIds.Contains(id))
            .Select(id => new ResourceLibraryCourse(GuidGenerator.Create(), id, courseId, tenantId))
            .ToList();
        if (toAdd.Count > 0)
        {
            await _courseLinkRepository.InsertManyAsync(toAdd, autoSave: true);
        }

        return validated.Count > 0 ? validated[0] : null;
    }

    public async Task<Guid?> SyncPracticumLibrariesAsync(
        Guid practicumId,
        IReadOnlyList<Guid>? libraryIds,
        Guid tenantId,
        Guid? courseId)
    {
        var requested = Normalize(libraryIds);
        if (requested.Count > 0)
        {
            var courseLibraryIds = await GetLibrariesContainingCoursesAsync(courseId);
            foreach (var libraryId in requested)
            {
                if (!courseLibraryIds.Contains(libraryId))
                {
                    throw new UserFriendlyException(
                        courseId.HasValue
                            ? "实训只能关联「包含该课程」的教学资源库。"
                            : "实训只能关联已有课程的教学资源库。");
                }

                // 可挂载性（本校主建 / 参与共建）仍走统一校验器
                await _attachmentValidator.ValidateAsync(libraryId, tenantId);
            }
        }

        var query = await _practicumLinkRepository.GetQueryableAsync();
        var existing = await AsyncExecuter.ToListAsync(query.Where(x => x.PracticumProjectId == practicumId));

        var toRemove = existing.Where(x => !requested.Contains(x.LibraryId)).ToList();
        if (toRemove.Count > 0)
        {
            await _practicumLinkRepository.DeleteManyAsync(toRemove, autoSave: true);
        }

        var existingIds = existing.Select(x => x.LibraryId).ToList();
        var toAdd = requested
            .Distinct()
            .Where(id => !existingIds.Contains(id))
            .Select(id => new ResourceLibraryPracticum(GuidGenerator.Create(), id, practicumId, tenantId))
            .ToList();
        if (toAdd.Count > 0)
        {
            await _practicumLinkRepository.InsertManyAsync(toAdd, autoSave: true);
        }

        return requested.Count > 0 ? requested[0] : null;
    }

    /// <summary>
    /// 取「已有课程」的库 Id 集合：关联表 ∪ 主库字段。
    /// <paramref name="courseId"/> 非空时只取包含该课程的库。
    /// </summary>
    private async Task<HashSet<Guid>> GetLibrariesContainingCoursesAsync(Guid? courseId)
    {
        var result = new HashSet<Guid>();
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var linkQuery = await _courseLinkRepository.GetQueryableAsync();
            var links = await AsyncExecuter.ToListAsync(
                courseId.HasValue
                    ? linkQuery.Where(x => x.CourseId == courseId.Value)
                    : linkQuery);

            foreach (var link in links)
            {
                result.Add(link.LibraryId);
            }

            var courseQuery = await _courseRepository.GetQueryableAsync();
            var legacyQuery = courseId.HasValue
                ? courseQuery.Where(x => x.Id == courseId.Value && x.LibraryId.HasValue)
                : courseQuery.Where(x => x.LibraryId.HasValue);

            var legacy = await AsyncExecuter.ToListAsync(
                legacyQuery.Select(x => x.LibraryId!.Value));
            foreach (var libraryId in legacy)
            {
                result.Add(libraryId);
            }
        }

        return result;
    }

    private static List<Guid> Normalize(IReadOnlyList<Guid>? libraryIds)
    {
        return (libraryIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
