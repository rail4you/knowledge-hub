using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Courses;

public class CourseAppService : KnowledgeHubAppService, ICourseAppService
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    private readonly IRepository<CourseMajor, Guid> _courseMajorRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CourseAppService> _logger;

    public CourseAppService(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Major, Guid> majorRepository,
        IRepository<CourseMajor, Guid> courseMajorRepository,
        ICurrentUser currentUser,
        ILogger<CourseAppService> logger)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _majorRepository = majorRepository;
        _courseMajorRepository = courseMajorRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid? ResolveTenantFilter(Guid? inputTenantId)
    {
        if (CurrentTenant.Id.HasValue)
            return CurrentTenant.Id.Value;
        return inputTenantId;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Create)]
    public async Task<CourseDto> CreateAsync(CreateUpdateCourseDto input)
    {
        var course = new Course(GuidGenerator.Create(), input.Title)
        {
            TenantId = CurrentTenant.Id
        };
        course.Description = input.Description;
        course.CoverImageUrl = input.CoverImageUrl;
        course.Semester = input.Semester;
        course.Credits = input.Credits;
        course.SemesterHours = input.SemesterHours;
        course.Difficulty = input.Difficulty;
        course.CategoryId = input.CategoryId;
        course.Status = input.Status;
        course.IsRecommended = input.IsRecommended;
        course.TeacherId = _currentUser.Id;

        await _courseRepository.InsertAsync(course);

        // 主从专业双写：MajorIds 为空即公共课
        NormalizeMajorInput(input, out var majorIds, out var primaryMajorId);
        await SyncCourseMajorsAsync(course, majorIds, primaryMajorId);
        await _courseRepository.UpdateAsync(course);

        return await MapToDtoAsync(course);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseDto> UpdateAsync(Guid id, CreateUpdateCourseDto input)
    {
        var course = await _courseRepository.FindAsync(id);
        if (course == null)
        {
            throw new Volo.Abp.UserFriendlyException("Course不存在");
        }

        course.Title = input.Title;
        course.Description = input.Description;
        course.CoverImageUrl = input.CoverImageUrl;
        course.Semester = input.Semester;
        course.Credits = input.Credits;
        course.SemesterHours = input.SemesterHours;
        course.Difficulty = input.Difficulty;
        course.CategoryId = input.CategoryId;
        course.Status = input.Status;
        course.IsRecommended = input.IsRecommended;

        NormalizeMajorInput(input, out var majorIds, out var primaryMajorId);
        await SyncCourseMajorsAsync(course, majorIds, primaryMajorId);

        await _courseRepository.UpdateAsync(course);

        return await MapToDtoAsync(course);
    }

    public async Task<CourseDto> GetAsync(Guid id)
    {
        var course = await _courseRepository.FindAsync(id);
        if (course == null)
        {
            return null;
        }
        return await MapToDtoAsync(course);
    }

    public async Task<PagedResultDto<CourseDto>> GetListAsync(PagedCourseRequestDto input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        List<Course> courses;
        int totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            query = query.WhereIf(tenantFilter.HasValue, x => x.TenantId == tenantFilter.Value)
                         .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                         .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                         .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                         .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
                         .WhereIf(input.Status.HasValue, x => x.Status == input.Status)
                         .WhereIf(input.IsRecommended.HasValue, x => x.IsRecommended == input.IsRecommended!.Value);
            query = await ApplyMajorFilterAsync(query, CollectTargetMajors(input.MajorId, input.MajorIds), tenantFilter);

            totalCount = await query.CountAsync();
            courses = await query.OrderByDescending(x => x.CreationTime)
                               .Skip(input.SkipCount)
                               .Take(input.MaxResultCount)
                               .ToListAsync();
        }

        var dtos = courses.Select(MapToDto).ToList();
        await AttachMajorsBatchAsync(dtos);

        return new PagedResultDto<CourseDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var links = await _courseMajorRepository.GetListAsync(x => x.CourseId == id);
        foreach (var link in links)
        {
            await _courseMajorRepository.DeleteAsync(link);
        }
        await _courseRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<AuditResultDto> AuditAsync(Guid courseId, AuditCourseDto input)
    {
        var course = await _courseRepository.FindAsync(courseId);
        if (course == null)
        {
            throw new Volo.Abp.UserFriendlyException("Course不存在");
        }

        if (input.Approved)
        {
            course.Status = CourseStatus.Published;
        }

        await _courseRepository.UpdateAsync(course);

        return new AuditResultDto
        {
            Success = true,
            Message = input.Approved ? "课程已审核通过并发布" : "课程审核未通过"
        };
    }

    public async Task<CourseDetailDto> GetDetailAsync(Guid id)
    {
        Course? course;
        using (DataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(id);
        }
        if (course == null)
        {
            return null;
        }

        var chapters = await _chapterRepository.GetListAsync(x => x.CourseId == id);
        var knowledgeResources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == id);

        // Get teacher name
        string? teacherName = null;
        if (course.TeacherId.HasValue)
        {
            var teacher = await _userRepository.FindAsync(course.TeacherId.Value);
            teacherName = teacher?.Name ?? teacher?.UserName;
        }

        // Get student count (cross-tenant query)
        int studentCount;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var studentCourseQuery = await _studentCourseRepository.GetQueryableAsync();
            studentCount = studentCourseQuery
                .Where(sc => sc.CourseId == id && sc.Status != StudentCourseStatus.Dropped)
                .Count();
        }

        // Get current user's enrollment status
        var currentUserId = _currentUser.Id;
        StudentCourse? enrollment = null;
        if (currentUserId.HasValue)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                enrollment = await _studentCourseRepository
                    .FirstOrDefaultAsync(sc => sc.CourseId == id && sc.StudentId == currentUserId.Value);
            }
        }

        // Build chapter tree with knowledge resources
        var chapterDtos = chapters
            .OrderBy(c => c.SortOrder)
            .Select(ch =>
            {
                var chResources = knowledgeResources
                    .Where(kr => kr.ChapterId == ch.Id)
                    .Select(kr => new KnowledgeResourceDto
                    {
                        Id = kr.Id,
                        CourseId = kr.CourseId,
                        ChapterId = kr.ChapterId,
                        Name = kr.Name,
                        Description = kr.Description,
                        Content = kr.Content,
                        Difficulty = kr.Difficulty,
                        ImportanceLevel = kr.ImportanceLevel,
                        SortOrder = kr.SortOrder,
                        Tags = kr.Tags,
                        ParentId = kr.ParentId,
                        ResourceId = kr.ResourceId
                    }).ToList();

                return new ChapterDto
                {
                    Id = ch.Id,
                    CourseId = ch.CourseId,
                    ParentId = ch.ParentId,
                    Title = ch.Title,
                    Description = ch.Description,
                    SortOrder = ch.SortOrder,
                    Children = new List<ChapterDto>(),
                    KnowledgeResources = chResources
                };
            })
            .ToList();

        // Organize flat chapter list into a tree based on ParentId
        var chapterTree = BuildChapterTree(chapterDtos);

        var detail = new CourseDetailDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CoverImageUrl = course.CoverImageUrl,
            MajorId = course.MajorId,
            Semester = course.Semester,
            Credits = course.Credits,
            SemesterHours = course.SemesterHours,
            Difficulty = course.Difficulty,
            Status = course.Status,
            IsRecommended = course.IsRecommended,
            TeacherId = course.TeacherId,
            CategoryId = course.CategoryId,
            TeacherName = teacherName,
            StudentCount = studentCount,
            ChapterCount = chapters.Count,
            IsEnrolled = enrollment != null,
            Progress = enrollment?.Progress ?? 0,
            Chapters = chapterTree
        };
        await AttachMajorsAsync(detail);
        return detail;
    }

    public async Task<PagedResultDto<CourseDto>> GetPublishedAsync(PagedCourseRequestDto input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);
        List<Course> courses;
        int totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            query = query.WhereIf(tenantFilter.HasValue, x => x.TenantId == tenantFilter.Value)
                         .Where(x => x.Status == CourseStatus.Published)
                         .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                         .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                         .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                         .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
                         .WhereIf(input.IsRecommended.HasValue, x => x.IsRecommended == input.IsRecommended!.Value);
            query = await ApplyMajorFilterAsync(query, CollectTargetMajors(input.MajorId, input.MajorIds), tenantFilter);

            totalCount = await query.CountAsync();
            courses = await query.OrderByDescending(x => x.CreationTime)
                               .Skip(input.SkipCount)
                               .Take(input.MaxResultCount)
                               .ToListAsync();
        }

        // 批量查询章节数和选课人数
        var courseIds = courses.Select(c => c.Id).ToList();
        var chapterQuery = await _chapterRepository.GetQueryableAsync();
        var chapterCounts = await AsyncExecuter.ToListAsync(
            chapterQuery.Where(ch => courseIds.Contains(ch.CourseId))
                .GroupBy(ch => ch.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() }));

        Dictionary<Guid, int> studentCountMap;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var studentQuery = await _studentCourseRepository.GetQueryableAsync();
            var studentCounts = await AsyncExecuter.ToListAsync(
                studentQuery.Where(sc => courseIds.Contains(sc.CourseId) && sc.Status != StudentCourseStatus.Dropped)
                    .GroupBy(sc => sc.CourseId)
                    .Select(g => new { CourseId = g.Key, Count = g.Count() }));
            studentCountMap = studentCounts.ToDictionary(x => x.CourseId, x => x.Count);
        }

        var chapterCountMap = chapterCounts.ToDictionary(x => x.CourseId, x => x.Count);

        var dtos = courses.Select(c =>
        {
            var dto = MapToDto(c);
            dto.ChapterCount = chapterCountMap.GetValueOrDefault(c.Id, 0);
            dto.StudentCount = studentCountMap.GetValueOrDefault(c.Id, 0);
            return dto;
        }).ToList();
        await AttachMajorsBatchAsync(dtos);

        return new PagedResultDto<CourseDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Enroll)]
    public async Task EnrollAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.UserFriendlyException("用户未登录");
        
        var existing = await _studentCourseRepository.FirstOrDefaultAsync(x => x.StudentId == studentId && x.CourseId == courseId);
        if (existing != null)
        {
            throw new Volo.Abp.UserFriendlyException("已经选修该课程");
        }
        
        var studentCourse = new StudentCourse(GuidGenerator.Create(), studentId, courseId);
        await _studentCourseRepository.InsertAsync(studentCourse);
    }

    [Authorize]
    public async Task DropAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.UserFriendlyException("用户未登录");
        
        var studentCourse = await _studentCourseRepository.FirstOrDefaultAsync(x => x.StudentId == studentId && x.CourseId == courseId);
        if (studentCourse == null)
        {
            throw new Volo.Abp.UserFriendlyException("未选修该课程");
        }
        
        studentCourse.Drop();
        await _studentCourseRepository.UpdateAsync(studentCourse);
    }

    [Authorize]
    public async Task<PagedResultDto<CourseDto>> GetMyCoursesAsync(PagedCourseRequestDto input)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.UserFriendlyException("用户未登录");
        
        var query = await _studentCourseRepository.GetQueryableAsync();
        query = query.Where(x => x.StudentId == studentId);

        var studentCourses = query.Skip(input.SkipCount)
                                   .Take(input.MaxResultCount)
                                   .ToList();

        var courseIds = studentCourses.Select(x => x.CourseId).ToList();
        var coursesQuery = await _courseRepository.GetQueryableAsync();
        var coursesBaseQuery = coursesQuery.Where(x => courseIds.Contains(x.Id))
                                  .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                                  .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                                  .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                                  .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId);
        // 我的课程走当前租户上下文，关联表同样走环境租户过滤
        var coursesFilteredQuery = await ApplyMajorFilterAsync(coursesBaseQuery, CollectTargetMajors(input.MajorId, input.MajorIds), null);
        var courses = coursesFilteredQuery.ToList();

        var dtos = courses.Select(MapToDto).ToList();
        await AttachMajorsBatchAsync(dtos);
        return new PagedResultDto<CourseDto>(courses.Count, dtos);
    }

    [Authorize]
    public async Task<PagedResultDto<CourseDto>> GetByFilterAsync(CourseFilterDto filter)
    {
        var tenantFilter = ResolveTenantFilter(filter.TenantId);
        List<Course> courses;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            query = query.WhereIf(tenantFilter.HasValue, x => x.TenantId == tenantFilter.Value)
                         .WhereIf(!string.IsNullOrWhiteSpace(filter.Filter), x => x.Title.Contains(filter.Filter))
                         .WhereIf(!string.IsNullOrWhiteSpace(filter.Semester), x => x.Semester == filter.Semester)
                         .WhereIf(filter.Difficulty.HasValue, x => x.Difficulty == filter.Difficulty)
                         .WhereIf(filter.CategoryId.HasValue, x => x.CategoryId == filter.CategoryId)
                         .WhereIf(filter.TeacherId.HasValue, x => x.TeacherId == filter.TeacherId)
                         .WhereIf(filter.Status.HasValue, x => x.Status == filter.Status)
                         .WhereIf(filter.IsRecommended.HasValue, x => x.IsRecommended == filter.IsRecommended!.Value);
            query = await ApplyMajorFilterAsync(query, CollectTargetMajors(filter.MajorId, filter.MajorIds), tenantFilter);

            courses = await query.OrderByDescending(x => x.CreationTime).ToListAsync();
        }
        var dtos = courses.Select(MapToDto).ToList();
        await AttachMajorsBatchAsync(dtos);

        return new PagedResultDto<CourseDto>(courses.Count, dtos);
    }

    public async Task<List<string>> GetSemestersAsync()
    {
        var tenantFilter = ResolveTenantFilter(null);
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            query = query.WhereIf(tenantFilter.HasValue, x => x.TenantId == tenantFilter.Value);
            return await query.Where(x => x.Semester != null)
                        .Select(x => x.Semester!)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToListAsync();
        }
    }

    private List<ChapterDto> BuildChapterTree(List<ChapterDto> chapters)
    {
        var lookup = chapters.ToDictionary(c => c.Id, c => c);
        var roots = new List<ChapterDto>();

        foreach (var chapter in chapters)
        {
            if (chapter.ParentId == null || !lookup.ContainsKey(chapter.ParentId.Value))
            {
                roots.Add(chapter);
            }
            else
            {
                var parent = lookup[chapter.ParentId.Value];
                parent.Children.Add(chapter);
            }
        }

        return roots;
    }

    private CourseDto MapToDto(Course course)
    {
        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CoverImageUrl = course.CoverImageUrl,
            MajorId = course.MajorId,
            Semester = course.Semester,
            Credits = course.Credits,
            SemesterHours = course.SemesterHours,
            Status = course.Status,
            Difficulty = course.Difficulty,
            IsRecommended = course.IsRecommended,
            TeacherId = course.TeacherId,
            CategoryId = course.CategoryId,
            CreationTime = course.CreationTime,
            CreatorId = course.CreatorId,
            LastModificationTime = course.LastModificationTime,
            LastModifierId = course.LastModifierId
        };
    }

    private async Task<CourseDto> MapToDtoAsync(Course course)
    {
        var dto = MapToDto(course);
        await AttachMajorsAsync(dto);
        return dto;
    }

    // ═══ 主从专业（多专业） helpers ═══

    /// <summary>
    /// 归一化写入输入：MajorId（老字段）视为主专业；MajorIds 为全量归属，空即公共课。
    /// 老客户端只传 MajorId 时退化为单专业行为。
    /// </summary>
    private static void NormalizeMajorInput(CreateUpdateCourseDto input, out List<Guid> majorIds, out Guid? primaryMajorId)
    {
        majorIds = (input.MajorIds ?? new List<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();
        if (input.MajorId.HasValue && input.MajorId.Value != Guid.Empty && !majorIds.Contains(input.MajorId.Value))
        {
            majorIds.Insert(0, input.MajorId.Value);
        }
        if (majorIds.Count == 0)
        {
            primaryMajorId = null;
            return;
        }
        primaryMajorId = input.MajorId.HasValue && majorIds.Contains(input.MajorId.Value)
            ? input.MajorId.Value
            : majorIds[0];
    }

    /// <summary>合并单选 + 多选筛选条件。</summary>
    private static List<Guid> CollectTargetMajors(Guid? majorId, List<Guid>? majorIds)
    {
        var list = (majorIds ?? new List<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();
        if (majorId.HasValue && majorId.Value != Guid.Empty && !list.Contains(majorId.Value))
        {
            list.Add(majorId.Value);
        }
        return list;
    }

    /// <summary>
    /// 按专业筛选：命中目标专业（含兼属、主从都算）或公共课（无任何专业归属）。
    /// 无筛选条件时返回原查询。tenantFilter 有值时在禁用租户过滤的上下文中手动隔离关联表。
    /// </summary>
    private async Task<IQueryable<Course>> ApplyMajorFilterAsync(
        IQueryable<Course> query, List<Guid> targetMajorIds, Guid? tenantFilter)
    {
        if (targetMajorIds.Count == 0)
        {
            return query;
        }
        var linkQuery = await _courseMajorRepository.GetQueryableAsync();
        if (tenantFilter.HasValue)
        {
            linkQuery = linkQuery.Where(x => x.TenantId == tenantFilter.Value);
        }
        var linkedMatched = await linkQuery
            .Where(x => targetMajorIds.Contains(x.MajorId))
            .Select(x => x.CourseId)
            .Distinct()
            .ToListAsync();
        var linkedAny = await linkQuery
            .Select(x => x.CourseId)
            .Distinct()
            .ToListAsync();
        return query.Where(x =>
            (x.MajorId.HasValue && targetMajorIds.Contains(x.MajorId.Value)) ||
            linkedMatched.Contains(x.Id) ||
            !linkedAny.Contains(x.Id));
    }

    /// <summary>
    /// 双写关联表 + 回写主专业。majorIds 为空即公共课（清关联、主专业置空）。
    /// </summary>
    private async Task SyncCourseMajorsAsync(Course course, List<Guid> majorIds, Guid? primaryMajorId)
    {
        course.MajorId = primaryMajorId;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var existing = (await _courseMajorRepository.GetQueryableAsync())
                .Where(x => x.CourseId == course.Id)
                .WhereIf(course.TenantId.HasValue, x => x.TenantId == course.TenantId!.Value)
                .WhereIf(!course.TenantId.HasValue, x => x.TenantId == null)
                .ToList();

            foreach (var stale in existing.Where(x => !majorIds.Contains(x.MajorId)))
            {
                await _courseMajorRepository.DeleteAsync(stale);
            }

            foreach (var mid in majorIds)
            {
                var isPrimary = primaryMajorId.HasValue && mid == primaryMajorId.Value;
                var link = existing.FirstOrDefault(x => x.MajorId == mid);
                if (link == null)
                {
                    await _courseMajorRepository.InsertAsync(
                        new CourseMajor(GuidGenerator.Create(), course.Id, mid, isPrimary)
                        {
                            TenantId = course.TenantId
                        });
                }
                else if (link.IsPrimary != isPrimary)
                {
                    link.IsPrimary = isPrimary;
                    await _courseMajorRepository.UpdateAsync(link);
                }
            }
        }
    }

    private async Task AttachMajorsAsync(CourseDto dto)
    {
        var dtos = new List<CourseDto> { dto };
        await AttachMajorsBatchAsync(dtos);
    }

    /// <summary>
    /// 批量填充 MajorIds/MajorNames（主专业排第一）并回填 MajorId/MajorName 兼容字段。
    /// </summary>
    private async Task AttachMajorsBatchAsync(List<CourseDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }
        var courseIds = dtos.Select(x => x.Id).Distinct().ToList();

        List<CourseMajor> links;
        using (DataFilter.Disable<IMultiTenant>())
        {
            links = (await _courseMajorRepository.GetQueryableAsync())
                .Where(x => courseIds.Contains(x.CourseId))
                .ToList();
        }

        var majorIds = links.Select(x => x.MajorId).Distinct().ToList();
        Dictionary<Guid, string> names = new();
        if (majorIds.Count > 0)
        {
            var majorQuery = await _majorRepository.GetQueryableAsync();
            names = await majorQuery
                .Where(x => majorIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name);
        }

        var linksByCourse = links
            .GroupBy(x => x.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreationTime).ToList());

        foreach (var dto in dtos)
        {
            if (!linksByCourse.TryGetValue(dto.Id, out var courseLinks) || courseLinks.Count == 0)
            {
                // 公共课：无归属专业
                dto.MajorId = null;
                dto.MajorName = null;
                dto.MajorIds = new List<Guid>();
                dto.MajorNames = new List<string>();
                continue;
            }
            var orderedIds = courseLinks.Select(x => x.MajorId).Distinct().ToList();
            dto.MajorIds = orderedIds;
            dto.MajorNames = orderedIds.Select(id => names.GetValueOrDefault(id)).Where(n => n != null).Cast<string>().ToList();
            dto.MajorId = orderedIds[0];
            dto.MajorName = names.GetValueOrDefault(orderedIds[0]);
        }
    }
}
