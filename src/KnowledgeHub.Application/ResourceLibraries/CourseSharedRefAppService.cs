using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Features;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.ResourceLibraries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.TenantManagement;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 跨校引用（V2.0 P4）。
///
/// <para><b>规则</b>：</para>
/// <list type="bullet">
/// <item><description>只能引用「已发布到库」的内容（<c>ResourcePublication.Published</c>）。</description></item>
/// <item><description>只能引用到本校可编辑的课程（<c>Course.TenantId == 当前租户</c>）。</description></item>
/// <item><description>引用生效后，<b>目标学校的课程学生</b>才可学习；来源校看不到对方学生明细。</description></item>
/// <item><description>撤销是实时生效的：播放/下载时按 <c>Status</c> 判定，不依赖缓存或签名 URL。</description></item>
/// </list>
/// </summary>
[IgnoreAntiforgeryToken]
public class CourseSharedRefAppService : KnowledgeHubAppService, ICourseSharedRefAppService
{
    private readonly IRepository<CourseSharedRef, Guid> _refRepository;
    private readonly IRepository<ResourceUsageEvent, Guid> _usageRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly IRepository<ResourcePublication, Guid> _publicationRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IFeatureChecker _featureChecker;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public CourseSharedRefAppService(
        IRepository<CourseSharedRef, Guid> refRepository,
        IRepository<ResourceUsageEvent, Guid> usageRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceLibrary, Guid> libraryRepository,
        IRepository<ResourcePublication, Guid> publicationRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IFeatureChecker featureChecker,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _refRepository = refRepository;
        _usageRepository = usageRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _resourceRepository = resourceRepository;
        _libraryRepository = libraryRepository;
        _publicationRepository = publicationRepository;
        _tenantRepository = tenantRepository;
        _featureChecker = featureChecker;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    /// <summary>把共建库里的内容引用进本校课程。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseSharedRefDto> CreateAsync(CreateCourseSharedRefDto input)
    {
        await EnsureFeatureAsync();

        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;

        // ① 目标课程必须是本校的、且当前用户可编辑
        var course = await _courseRepository.FindAsync(input.CourseId)
                     ?? throw new UserFriendlyException("课程不存在。");
        if (course.TenantId != tenantId)
        {
            throw new UserFriendlyException("只能引用到本校课程。");
        }

        if (!IsTenantAdmin() && course.TeacherId != userId)
        {
            throw new UserFriendlyException("只有课程负责人或院校管理员可以引用资源。");
        }

        // ② 内容必须已发布到该库（准入审核通过）
        var publication = await FindPublishedAsync(input.LibraryId, input.ContentId)
                          ?? throw new UserFriendlyException("该内容尚未在该资源库发布（或已下架），不能引用。");

        // ③ 不能重复引用同一内容到同一课程
        var existingQuery = await _refRepository.GetQueryableAsync();
        var duplicated = await _asyncExecuter.AnyAsync(
            existingQuery.Where(x => x.CourseId == input.CourseId
                                     && x.ContentId == input.ContentId
                                     && x.Status == CourseSharedRefStatus.Active));
        if (duplicated)
        {
            throw new UserFriendlyException("该资源已引用到本课程。");
        }

        var library = await FindLibraryAsync(input.LibraryId);
        var reference = new CourseSharedRef(
            GuidGenerator.Create(),
            tenantId,
            course.Id,
            input.ChapterId,
            input.LibraryId,
            input.ContentId,
            input.ContentVersionId,
            library?.OwnerTenantId ?? publication.ContentTenantId ?? Guid.Empty);

        await _refRepository.InsertAsync(reference, autoSave: true);

        // 引用事件（去重统计用）
        await RecordUsageAsync(tenantId, course.Id, reference.Id, input.ContentId, "reference");

        return await ToDtoAsync(reference, tenantId, userId);
    }

    /// <summary>撤销引用（引用方本校可操作；来源下架时也可由来源校触发）。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseSharedRefDto> RevokeAsync(Guid id, string? reason = null)
    {
        var reference = await FindRefAsync(id);
        var tenantId = CurrentTenant.Id;

        if (reference.TargetTenantId != tenantId && reference.SourceTenantId != tenantId && !IsTenantAdmin())
        {
            throw new UserFriendlyException("无权撤销该引用。");
        }

        if (reference.Status == CourseSharedRefStatus.Revoked)
        {
            return await ToDtoAsync(reference, tenantId, CurrentUser.Id);
        }

        reference.Revoke(CurrentUser.Id, reason);
        await _refRepository.UpdateAsync(reference, autoSave: true);

        return await ToDtoAsync(reference, tenantId, CurrentUser.Id);
    }

    /// <summary>升级到新版本（旧版本号记入 ExtraProperties）。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseSharedRefDto> UpgradeAsync(Guid id, UpgradeCourseSharedRefDto input)
    {
        var reference = await FindRefAsync(id);
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");

        if (reference.TargetTenantId != tenantId)
        {
            throw new UserFriendlyException("只有引用方学校可以升级引用版本。");
        }

        reference.Upgrade(input.ContentVersionId);
        await _refRepository.UpdateAsync(reference, autoSave: true);

        return await ToDtoAsync(reference, tenantId, CurrentUser.Id);
    }

    /// <summary>引用列表（引用管理页）：可按方向（我引用的 / 被引用的）与状态过滤。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Default)]
    public async Task<PagedResultDto<CourseSharedRefDto>> GetListAsync(CourseSharedRefListRequestDto input)
    {
        var tenantId = CurrentTenant.Id;
        var query = await _refRepository.GetQueryableAsync();

        // 「我引用的」= 我是接收校；「被引用的」= 我是来源校。
        // 来源校视角需要跨租户读（引用行归属接收校），因此整体关闭租户过滤器后按方向过滤。
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var direction = (input.Direction ?? "all").ToLowerInvariant();
            query = direction switch
            {
                "outgoing" => query.Where(x => x.SourceTenantId == tenantId),
                "incoming" => query.Where(x => x.TargetTenantId == tenantId),
                _ => query.Where(x => x.TargetTenantId == tenantId || x.SourceTenantId == tenantId)
            };

            if (input.CourseId.HasValue)
            {
                query = query.Where(x => x.CourseId == input.CourseId.Value);
            }

            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            var total = await _asyncExecuter.CountAsync(query);
            var list = await _asyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime)
                     .Skip(input.SkipCount)
                     .Take(input.MaxResultCount));

            var dtos = new List<CourseSharedRefDto>(list.Count);
            foreach (var item in list)
            {
                dtos.Add(await ToDtoAsync(item, tenantId, CurrentUser.Id));
            }

            return new PagedResultDto<CourseSharedRefDto>(total, dtos);
        }
    }

    /// <summary>某课程的全部引用（章节编辑器 / 课程详情用）。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Default)]
    public async Task<List<CourseSharedRefDto>> GetByCourseAsync(Guid courseId)
    {
        var tenantId = CurrentTenant.Id;
        var query = await _refRepository.GetQueryableAsync();
        var list = await _asyncExecuter.ToListAsync(
            query.Where(x => x.CourseId == courseId).OrderByDescending(x => x.CreationTime));

        var result = new List<CourseSharedRefDto>(list.Count);
        foreach (var item in list)
        {
            result.Add(await ToDtoAsync(item, tenantId, CurrentUser.Id));
        }

        return result;
    }

    /// <summary>
    /// 播放/学习前的访问判定（学生路径）。
    /// 学生必须：本校 = 接收校 且 已选该课程 且 引用生效。
    /// </summary>
    [AllowAnonymous]
    public async Task<CourseSharedRefAccessDto> CheckAccessAsync(Guid refId)
    {
        var reference = await FindRefAsync(refId);
        var tenantId = CurrentTenant.Id;

        if (reference.Status != CourseSharedRefStatus.Active)
        {
            return new CourseSharedRefAccessDto
            {
                CanAccess = false,
                Reason = "该资源已撤销引用或下架，请联系课程教师更换。",
                ResourceId = reference.ContentId,
                LibraryId = reference.LibraryId,
                SourceTenantId = reference.SourceTenantId
            };
        }

        if (reference.TargetTenantId != tenantId)
        {
            return new CourseSharedRefAccessDto
            {
                CanAccess = false,
                Reason = "该引用不属于当前学校。",
                ResourceId = reference.ContentId
            };
        }

        var userId = CurrentUser.Id;
        var isAdmin = IsTenantAdmin();

        var enrolled = false;
        if (userId.HasValue)
        {
            var enrollQuery = await _studentCourseRepository.GetQueryableAsync();
            enrolled = await _asyncExecuter.AnyAsync(
                enrollQuery.Where(x => x.CourseId == reference.CourseId
                                       && x.StudentId == userId.Value
                                       && x.Status != StudentCourseStatus.Dropped));
        }

        if (!enrolled && !isAdmin)
        {
            return new CourseSharedRefAccessDto
            {
                CanAccess = false,
                Reason = "你尚未加入该课程，无法学习此资源。",
                ResourceId = reference.ContentId
            };
        }

        // 记录学习事件（幂等：同一用户同一引用每天只记一次由调用方决定 EventId）
        await RecordUsageAsync(tenantId, reference.CourseId, reference.Id, reference.ContentId, "study");

        return new CourseSharedRefAccessDto
        {
            CanAccess = true,
            ResourceId = reference.ContentId,
            LibraryId = reference.LibraryId,
            SourceTenantId = reference.SourceTenantId
        };
    }

    /// <summary>跨校引用统计（来源校视角：我的内容被谁用了多少）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<SharedRefStatisticsDto> GetStatisticsAsync()
    {
        var tenantId = CurrentTenant.Id;
        var query = await _refRepository.GetQueryableAsync();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var all = await _asyncExecuter.ToListAsync(
                query.Where(x => x.SourceTenantId == tenantId || x.TargetTenantId == tenantId));

            var active = all.Where(x => x.Status == CourseSharedRefStatus.Active).ToList();

            var usageQuery = await _usageRepository.GetQueryableAsync();
            var usage = await _asyncExecuter.ToListAsync(
                usageQuery.Where(x => x.Type == "study" && x.SchoolId == tenantId));

            return new SharedRefStatisticsDto
            {
                ReferencedSchoolCount = active
                    .Where(x => x.SourceTenantId == tenantId)
                    .Select(x => x.TargetTenantId).Distinct().Count(),
                ReferencedCourseCount = active
                    .Where(x => x.SourceTenantId == tenantId)
                    .Select(x => x.CourseId).Distinct().Count(),
                ActiveRefCount = active.Count,
                RevokedRefCount = all.Count(x => x.Status == CourseSharedRefStatus.Revoked),
                LearnerCount = usage.Select(x => x.UserId).Where(x => x.HasValue).Distinct().Count()
            };
        }
    }

    /// <summary>上报使用事件（幂等：EventId 唯一）。</summary>
    [AllowAnonymous]
    public async Task<bool> ReportUsageAsync(ReportUsageEventDto input)
    {
        if (input.EventId == Guid.Empty)
        {
            throw new UserFriendlyException("EventId 不能为空（用于幂等去重）。");
        }

        var query = await _usageRepository.GetQueryableAsync();
        var exists = await _asyncExecuter.AnyAsync(query.Where(x => x.EventId == input.EventId));
        if (exists)
        {
            return false;
        }

        await _usageRepository.InsertAsync(new ResourceUsageEvent(GuidGenerator.Create())
        {
            EventId = input.EventId,
            SchoolId = CurrentTenant.Id,
            CourseId = input.CourseId,
            RefId = input.RefId,
            ResourceId = input.ResourceId,
            UserId = CurrentUser.Id,
            Type = string.IsNullOrWhiteSpace(input.Type) ? "study" : input.Type,
            OccurredAt = DateTime.UtcNow,
            TenantId = CurrentTenant.Id
        }, autoSave: true);

        return true;
    }

    // ── 内部 ──

    private async Task EnsureFeatureAsync()
    {
        if (!await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.CrossSchoolRef))
        {
            throw new UserFriendlyException("跨校引用功能未对本校开通（请联系平台管理员开启）。");
        }
    }

    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }

    private async Task<CourseSharedRef> FindRefAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _refRepository.FindAsync(id)
                   ?? throw new UserFriendlyException("引用记录不存在。");
        }
    }

    private async Task<ResourcePublication?> FindPublishedAsync(Guid libraryId, Guid contentId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _publicationRepository.GetQueryableAsync();
            return await _asyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.LibraryId == libraryId
                                 && x.ContentId == contentId
                                 && x.ContentType == LibraryContentType.Resource
                                 && x.Status == ResourcePublicationStatus.Published));
        }
    }

    private async Task<ResourceLibrary?> FindLibraryAsync(Guid libraryId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _libraryRepository.FindAsync(libraryId);
        }
    }

    private async Task<CourseSharedRefDto> ToDtoAsync(CourseSharedRef reference, Guid? tenantId, Guid? userId)
    {
        string? courseTitle = null;
        string? libraryName = null;
        string? contentName = null;
        string? sourceTenantName = null;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            courseTitle = (await _courseRepository.FindAsync(reference.CourseId))?.Title;
            libraryName = (await _libraryRepository.FindAsync(reference.LibraryId))?.Name;
            contentName = (await _resourceRepository.FindAsync(reference.ContentId))?.Name;
            sourceTenantName = (await _tenantRepository.FindAsync(reference.SourceTenantId))?.Name;
        }

        return new CourseSharedRefDto
        {
            Id = reference.Id,
            TargetTenantId = reference.TargetTenantId,
            CourseId = reference.CourseId,
            CourseTitle = courseTitle,
            ChapterId = reference.ChapterId,
            LibraryId = reference.LibraryId,
            LibraryName = libraryName,
            ContentType = reference.ContentType,
            ContentId = reference.ContentId,
            ContentName = contentName,
            ContentVersionId = reference.ContentVersionId,
            SourceTenantId = reference.SourceTenantId,
            SourceTenantName = sourceTenantName,
            Status = reference.Status,
            StatusName = reference.Status == CourseSharedRefStatus.Active ? "生效中" : "已撤销",
            RevokedReason = reference.RevokedReason,
            CreationTime = reference.CreationTime,
            CanManage = reference.TargetTenantId == tenantId
                        && (IsTenantAdmin() || userId.HasValue)
        };
    }

    private async Task RecordUsageAsync(Guid? schoolId, Guid? courseId, Guid? refId, Guid? resourceId, string type)
    {
        try
        {
            await _usageRepository.InsertAsync(new ResourceUsageEvent(GuidGenerator.Create())
            {
                EventId = GuidGenerator.Create(),
                SchoolId = schoolId,
                CourseId = courseId,
                RefId = refId,
                ResourceId = resourceId,
                UserId = CurrentUser.Id,
                Type = type,
                OccurredAt = DateTime.UtcNow,
                TenantId = schoolId
            }, autoSave: true);
        }
        catch
        {
            // 统计失败不影响主流程
        }
    }
}

/// <summary>跨校引用 AppService 契约（与实现同文件声明，便于对照阅读）</summary>
public interface ICourseSharedRefAppService
{
    Task<CourseSharedRefDto> CreateAsync(CreateCourseSharedRefDto input);
    Task<CourseSharedRefDto> RevokeAsync(Guid id, string? reason = null);
    Task<CourseSharedRefDto> UpgradeAsync(Guid id, UpgradeCourseSharedRefDto input);
    Task<List<CourseSharedRefDto>> GetByCourseAsync(Guid courseId);
    Task<PagedResultDto<CourseSharedRefDto>> GetListAsync(CourseSharedRefListRequestDto input);
    Task<CourseSharedRefAccessDto> CheckAccessAsync(Guid refId);
    Task<SharedRefStatisticsDto> GetStatisticsAsync();
    Task<bool> ReportUsageAsync(ReportUsageEventDto input);
}
