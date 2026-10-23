using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程共享 AppService。
///
/// <para>
/// 需求「租户下的课程和资源是其他学校共享的，需要课程和资源有共享到『社会学校』的功能」的课程侧实现。
/// </para>
/// <para>
/// 共享记录按源租户隔离（<c>TenantId = SourceTenantId</c>），目标租户查询时禁用租户过滤器，
/// 按 <c>TargetTenantId</c> 精确匹配 —— 与 <c>ResourceShareAppService</c> 同一套机制。
/// </para>
/// </summary>
public class CourseShareAppService : KnowledgeHubAppService, ICourseShareAppService
{
    private readonly ICourseShareRepository _shareRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IPublicSchoolProvider _publicSchoolProvider;
    private readonly IDataFilter _dataFilter;

    public CourseShareAppService(
        ICourseShareRepository shareRepository,
        ICourseRepository courseRepository,
        IMajorRepository majorRepository,
        ITenantRepository tenantRepository,
        ITenantInfoRepository tenantInfoRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IPublicSchoolProvider publicSchoolProvider,
        IDataFilter dataFilter)
    {
        _shareRepository = shareRepository;
        _courseRepository = courseRepository;
        _majorRepository = majorRepository;
        _tenantRepository = tenantRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _userRepository = userRepository;
        _publicSchoolProvider = publicSchoolProvider;
        _dataFilter = dataFilter;
    }

    /// <summary>把课程共享给一个或多个目标学校。</summary>
    [Authorize(KnowledgeHubPermissions.Resources.Share)]
    public virtual async Task<List<CourseShareDto>> ShareAsync(CreateCourseShareDto input)
    {
        if (input.TargetTenantIds == null || input.TargetTenantIds.Count == 0)
        {
            throw new UserFriendlyException("请至少选择一个目标学校。");
        }

        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知。");
        var course = await _courseRepository.GetAsync(input.CourseId);
        if (course.TenantId != sourceTenantId)
        {
            throw new UserFriendlyException("只能共享本租户的课程。");
        }

        var validTargetIds = input.TargetTenantIds
            .Where(t => t != Guid.Empty && t != sourceTenantId)
            .Distinct()
            .ToList();
        if (validTargetIds.Count == 0)
        {
            throw new UserFriendlyException("目标学校不能为空或本租户。");
        }

        var tenants = await _tenantRepository.GetListAsync();
        var existingTenantIds = tenants.Select(t => t.Id).ToHashSet();
        var invalid = validTargetIds.Where(t => !existingTenantIds.Contains(t)).ToList();
        if (invalid.Count > 0)
        {
            throw new UserFriendlyException($"目标学校无效：{string.Join(", ", invalid)}");
        }

        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var results = new List<CourseShareDto>();

        foreach (var targetTenantId in validTargetIds)
        {
            if (await _shareRepository.ExistsAsync(input.CourseId, targetTenantId))
            {
                continue;
            }

            var share = new CourseShare(
                GuidGenerator.Create(),
                input.CourseId,
                sourceTenantId,
                targetTenantId,
                userId,
                input.Note);

            await _shareRepository.InsertAsync(share);
            results.Add(ToShareDto(share, course.Title));
        }

        await FillMetaAsync(results);
        return results;
    }

    /// <summary>把课程一键共享到平台唯一「社会学校」。</summary>
    [Authorize(KnowledgeHubPermissions.Resources.Share)]
    public virtual async Task<CourseShareDto> ShareToPublicSchoolAsync(Guid courseId, string? note)
    {
        var publicSchoolTenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();
        if (publicSchoolTenantId == null)
        {
            throw new UserFriendlyException("平台尚未配置「社会学校」，请先在【学校设置】中指定。");
        }

        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知。");
        if (publicSchoolTenantId.Value == sourceTenantId)
        {
            throw new UserFriendlyException("当前租户就是社会学校，无需共享。");
        }

        var course = await _courseRepository.GetAsync(courseId);
        if (course.TenantId != sourceTenantId)
        {
            throw new UserFriendlyException("只能共享本租户的课程。");
        }

        if (await _shareRepository.ExistsAsync(courseId, publicSchoolTenantId.Value))
        {
            throw new UserFriendlyException("该课程已共享到社会学校。");
        }

        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var share = new CourseShare(
            GuidGenerator.Create(),
            courseId,
            sourceTenantId,
            publicSchoolTenantId.Value,
            userId,
            note);

        await _shareRepository.InsertAsync(share, autoSave: true);

        var dto = ToShareDto(share, course.Title);
        await FillMetaAsync(new List<CourseShareDto> { dto });
        return dto;
    }

    /// <summary>取消共享。</summary>
    [Authorize(KnowledgeHubPermissions.Resources.ManageShare)]
    public virtual async Task UnshareAsync(Guid courseId, Guid targetTenantId)
    {
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知。");
        var shares = await _shareRepository.GetByCourseAsync(courseId);
        var share = shares.FirstOrDefault(s => s.TargetTenantId == targetTenantId);
        if (share == null)
        {
            throw new UserFriendlyException("共享记录不存在。");
        }

        await _shareRepository.DeleteAsync(share, autoSave: true);
    }

    /// <summary>查看某课程的共享情况。</summary>
    [Authorize(KnowledgeHubPermissions.Resources.Share)]
    public virtual async Task<List<CourseShareDto>> GetSharesAsync(Guid courseId)
    {
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知。");
        var course = await _courseRepository.GetAsync(courseId);
        if (course.TenantId != sourceTenantId)
        {
            throw new UserFriendlyException("只能查看本租户课程的共享情况。");
        }

        var shares = await _shareRepository.GetByCourseAsync(courseId);
        var dtos = shares.Select(s => ToShareDto(s, course.Title)).ToList();
        await FillMetaAsync(dtos);
        return dtos;
    }

    /// <summary>共享给我的课程列表。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Default)]
    public virtual async Task<PagedResultDto<SharedCourseDto>> GetSharedToMeAsync(SharedCourseListQueryDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知。");

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var shares = await _shareRepository.GetByTargetTenantAsync(currentTenantId);
            if (shares.Count == 0)
            {
                return new PagedResultDto<SharedCourseDto>(0, new List<SharedCourseDto>());
            }

            var courseIds = shares.Select(s => s.CourseId).Distinct().ToList();
            var courseQuery = await _courseRepository.GetQueryableAsync();
            courseQuery = courseQuery.Where(c => courseIds.Contains(c.Id));

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var f = input.Filter.ToLower();
                courseQuery = courseQuery.Where(c => c.Title != null && c.Title.ToLower().Contains(f));
            }

            if (input.MajorId.HasValue)
            {
                courseQuery = courseQuery.Where(c => c.MajorId == input.MajorId.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(courseQuery);
            var courses = await AsyncExecuter.ToListAsync(
                courseQuery.OrderByDescending(c => c.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            var shareByCourse = shares
                .GroupBy(s => s.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SharedAt).First());

            var dtos = courses.Select(c =>
            {
                var share = shareByCourse[c.Id];
                return new SharedCourseDto
                {
                    Id = c.Id,
                    ShareId = share.Id,
                    CourseId = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverImageUrl = c.CoverImageUrl,
                    MajorId = c.MajorId,
                    LibraryId = c.LibraryId,
                    SourceTenantId = c.TenantId ?? Guid.Empty,
                    SharedAt = share.SharedAt,
                    SharedByUserId = share.SharedByUserId
                };
            }).ToList();

            await FillSharedCourseMetaAsync(dtos);
            return new PagedResultDto<SharedCourseDto>(totalCount, dtos);
        }
    }

    /// <summary>我共享出去的课程列表。</summary>
    [Authorize(KnowledgeHubPermissions.Courses.Default)]
    public virtual async Task<PagedResultDto<SharedCourseDto>> GetSharedByMeAsync(SharedCourseListQueryDto input)
    {
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知。");

        var allShares = await AsyncExecuter.ToListAsync(
            (await _shareRepository.GetQueryableAsync()).Where(x => x.SourceTenantId == sourceTenantId));

        if (allShares.Count == 0)
        {
            return new PagedResultDto<SharedCourseDto>(0, new List<SharedCourseDto>());
        }

        var courseIds = allShares.Select(s => s.CourseId).Distinct().ToList();
        var courseQuery = await _courseRepository.GetQueryableAsync();
        courseQuery = courseQuery.Where(c => courseIds.Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            courseQuery = courseQuery.Where(c => c.Title != null && c.Title.ToLower().Contains(f));
        }

        if (input.MajorId.HasValue)
        {
            courseQuery = courseQuery.Where(c => c.MajorId == input.MajorId.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(courseQuery);
        var courses = await AsyncExecuter.ToListAsync(
            courseQuery.OrderByDescending(c => c.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = courses.Select(c =>
        {
            var share = allShares.Where(s => s.CourseId == c.Id).OrderByDescending(s => s.SharedAt).First();
            return new SharedCourseDto
            {
                Id = c.Id,
                ShareId = share.Id,
                CourseId = c.Id,
                Title = c.Title,
                Description = c.Description,
                CoverImageUrl = c.CoverImageUrl,
                MajorId = c.MajorId,
                LibraryId = c.LibraryId,
                SourceTenantId = c.TenantId ?? sourceTenantId,
                SharedAt = share.SharedAt,
                SharedByUserId = share.SharedByUserId
            };
        }).ToList();

        await FillSharedCourseMetaAsync(dtos);
        return new PagedResultDto<SharedCourseDto>(totalCount, dtos);
    }

    private static CourseShareDto ToShareDto(CourseShare share, string? courseTitle)
    {
        return new CourseShareDto
        {
            Id = share.Id,
            CourseId = share.CourseId,
            CourseTitle = courseTitle,
            SourceTenantId = share.SourceTenantId,
            TargetTenantId = share.TargetTenantId,
            SharedByUserId = share.SharedByUserId,
            SharedAt = share.SharedAt,
            Note = share.Note
        };
    }

    private async Task FillMetaAsync(List<CourseShareDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var tenantIds = dtos.SelectMany(d => new[] { d.SourceTenantId, d.TargetTenantId })
            .Where(t => t != Guid.Empty)
            .Distinct()
            .ToList();
        var tenantNames = await ResolveTenantNamesAsync(tenantIds);

        var userIds = dtos.Select(d => d.SharedByUserId).Where(u => u != Guid.Empty).Distinct().ToList();
        var userNames = await ResolveUserNamesAsync(userIds);

        foreach (var dto in dtos)
        {
            if (tenantNames.TryGetValue(dto.SourceTenantId, out var sn))
            {
                dto.SourceTenantName = sn;
            }

            if (tenantNames.TryGetValue(dto.TargetTenantId, out var tn))
            {
                dto.TargetTenantName = tn;
            }

            if (userNames.TryGetValue(dto.SharedByUserId, out var un))
            {
                dto.SharedByUserName = un;
            }
        }
    }

    private async Task FillSharedCourseMetaAsync(List<SharedCourseDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var tenantIds = dtos.Select(d => d.SourceTenantId).Where(t => t != Guid.Empty).Distinct().ToList();
        var tenantNames = await ResolveTenantNamesAsync(tenantIds);

        var userIds = dtos.Select(d => d.SharedByUserId).Where(u => u != Guid.Empty).Distinct().ToList();
        var userNames = await ResolveUserNamesAsync(userIds);

        var majorIds = dtos.Select(d => d.MajorId).Where(m => m.HasValue).Select(m => m!.Value).Distinct().ToList();
        var majorNames = new Dictionary<Guid, string>();
        if (majorIds.Count > 0)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var majors = await AsyncExecuter.ToListAsync(
                    (await _majorRepository.GetQueryableAsync()).Where(x => majorIds.Contains(x.Id)));
                foreach (var major in majors)
                {
                    majorNames[major.Id] = major.Name;
                }
            }
        }

        foreach (var dto in dtos)
        {
            if (tenantNames.TryGetValue(dto.SourceTenantId, out var tn))
            {
                dto.SourceTenantName = tn;
            }

            if (userNames.TryGetValue(dto.SharedByUserId, out var un))
            {
                dto.SharedByUserName = un;
            }

            if (dto.MajorId.HasValue && majorNames.TryGetValue(dto.MajorId.Value, out var mn))
            {
                dto.MajorName = mn;
            }
        }
    }

    private async Task<Dictionary<Guid, string>> ResolveTenantNamesAsync(List<Guid> tenantIds)
    {
        var result = new Dictionary<Guid, string>();
        if (tenantIds.Count == 0)
        {
            return result;
        }

        foreach (var tenantId in tenantIds)
        {
            var info = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
            if (info != null)
            {
                result[tenantId] = info.Name;
                continue;
            }

            var tenant = await _tenantRepository.FindAsync(tenantId);
            if (tenant != null)
            {
                result[tenantId] = tenant.Name;
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(List<Guid> userIds)
    {
        var result = new Dictionary<Guid, string>();
        if (userIds.Count == 0)
        {
            return result;
        }

        var users = await AsyncExecuter.ToListAsync(
            (await _userRepository.GetQueryableAsync()).Where(u => userIds.Contains(u.Id)));
        foreach (var user in users)
        {
            var display = string.Join(" ", new[] { user.Name, user.Surname }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            result[user.Id] = !string.IsNullOrWhiteSpace(display)
                ? display
                : (user.UserName ?? user.Id.ToString());
        }

        return result;
    }
}
