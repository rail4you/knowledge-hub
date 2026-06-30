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

public class CourseAppService : ApplicationService, ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CourseAppService> _logger;

    public CourseAppService(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Major, Guid> majorRepository,
        ICurrentUser currentUser,
        ILogger<CourseAppService> logger)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _majorRepository = majorRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Create)]
    public async Task<CourseDto> CreateAsync(CreateUpdateCourseDto input)
    {
        var course = new Course(GuidGenerator.Create(), input.Title);
        course.Description = input.Description;
        course.CoverImageUrl = input.CoverImageUrl;
        course.MajorId = input.MajorId;
        course.Semester = input.Semester;
        course.Credits = input.Credits;
        course.SemesterHours = input.SemesterHours;
        course.Difficulty = input.Difficulty;
        course.CategoryId = input.CategoryId;
        course.Status = input.Status;
        course.TeacherId = _currentUser.Id;

        await _courseRepository.InsertAsync(course);

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
        course.MajorId = input.MajorId;
        course.Semester = input.Semester;
        course.Credits = input.Credits;
        course.SemesterHours = input.SemesterHours;
        course.Difficulty = input.Difficulty;
        course.CategoryId = input.CategoryId;
        course.Status = input.Status;

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
        var query = await _courseRepository.GetQueryableAsync();
        query = query.WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                     .WhereIf(input.MajorId.HasValue, x => x.MajorId == input.MajorId.Value)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                     .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                     .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
                     .WhereIf(input.Status.HasValue, x => x.Status == input.Status);

        var totalCount = query.Count();
        var courses = query.OrderByDescending(x => x.CreationTime)
                           .Skip(input.SkipCount)
                           .Take(input.MaxResultCount)
                           .ToList();

        var majorNames = await ResolveMajorNamesAsync(courses.Select(x => x.MajorId));
        return new PagedResultDto<CourseDto>(
            totalCount,
            courses.Select(x => MapToDtoWithMajor(x, majorNames)).ToList()
        );
    }

    [Authorize(KnowledgeHubPermissions.Courses.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _courseRepository.DeleteAsync(id);
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

        return new CourseDetailDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CoverImageUrl = course.CoverImageUrl,
            MajorId = course.MajorId,
            MajorName = await ResolveMajorNameAsync(course.MajorId),
            Semester = course.Semester,
            Credits = course.Credits,
            SemesterHours = course.SemesterHours,
            Difficulty = course.Difficulty,
            Status = course.Status,
            TeacherId = course.TeacherId,
            CategoryId = course.CategoryId,
            TeacherName = teacherName,
            StudentCount = studentCount,
            ChapterCount = chapters.Count,
            IsEnrolled = enrollment != null,
            Progress = enrollment?.Progress ?? 0,
            Chapters = chapterTree
        };
    }

    public async Task<PagedResultDto<CourseDto>> GetPublishedAsync(PagedCourseRequestDto input)
    {
        var query = await _courseRepository.GetQueryableAsync();
        query = query.Where(x => x.Status == CourseStatus.Published)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                     .WhereIf(input.MajorId.HasValue, x => x.MajorId == input.MajorId.Value)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                     .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                     .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId);

        var totalCount = query.Count();
        var courses = query.OrderByDescending(x => x.CreationTime)
                           .Skip(input.SkipCount)
                           .Take(input.MaxResultCount)
                           .ToList();

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
        var majorNames = await ResolveMajorNamesAsync(courses.Select(x => x.MajorId));

        return new PagedResultDto<CourseDto>(
            totalCount,
            courses.Select(c => new CourseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CoverImageUrl = c.CoverImageUrl,
                MajorId = c.MajorId,
                MajorName = majorNames.GetValueOrDefault(c.MajorId ?? Guid.Empty),
                Semester = c.Semester,
                Credits = c.Credits,
                SemesterHours = c.SemesterHours,
                Status = c.Status,
                Difficulty = c.Difficulty,
                TeacherId = c.TeacherId,
                CategoryId = c.CategoryId,
                ChapterCount = chapterCountMap.GetValueOrDefault(c.Id, 0),
                StudentCount = studentCountMap.GetValueOrDefault(c.Id, 0),
                CreationTime = c.CreationTime,
                CreatorId = c.CreatorId,
                LastModificationTime = c.LastModificationTime,
                LastModifierId = c.LastModifierId
            }).ToList()
        );
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
        var courses = coursesQuery.Where(x => courseIds.Contains(x.Id))
                                  .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                                  .WhereIf(input.MajorId.HasValue, x => x.MajorId == input.MajorId.Value)
                                  .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                                  .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                                  .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
                                  .ToList();

        var majorNames = await ResolveMajorNamesAsync(courses.Select(x => x.MajorId));
        return new PagedResultDto<CourseDto>(
            courses.Count,
            courses.Select(x => MapToDtoWithMajor(x, majorNames)).ToList()
        );
    }

    [Authorize]
    public async Task<PagedResultDto<CourseDto>> GetByFilterAsync(CourseFilterDto filter)
    {
        var query = await _courseRepository.GetQueryableAsync();
        query = query.WhereIf(!string.IsNullOrWhiteSpace(filter.Filter), x => x.Title.Contains(filter.Filter))
                     .WhereIf(filter.MajorId.HasValue, x => x.MajorId == filter.MajorId.Value)
                     .WhereIf(!string.IsNullOrWhiteSpace(filter.Semester), x => x.Semester == filter.Semester)
                     .WhereIf(filter.Difficulty.HasValue, x => x.Difficulty == filter.Difficulty)
                     .WhereIf(filter.CategoryId.HasValue, x => x.CategoryId == filter.CategoryId)
                     .WhereIf(filter.TeacherId.HasValue, x => x.TeacherId == filter.TeacherId)
                     .WhereIf(filter.Status.HasValue, x => x.Status == filter.Status);

        var courses = query.OrderByDescending(x => x.CreationTime).ToList();
        var majorNames = await ResolveMajorNamesAsync(courses.Select(x => x.MajorId));

        return new PagedResultDto<CourseDto>(
            courses.Count,
            courses.Select(x => MapToDtoWithMajor(x, majorNames)).ToList()
        );
    }

    public async Task<List<string>> GetSemestersAsync()
    {
        var query = await _courseRepository.GetQueryableAsync();
        return query.Where(x => x.Semester != null)
                    .Select(x => x.Semester!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
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
        dto.MajorName = await ResolveMajorNameAsync(course.MajorId);
        return dto;
    }

    private CourseDto MapToDtoWithMajor(Course course, Dictionary<Guid, string> majorNames)
    {
        var dto = MapToDto(course);
        if (course.MajorId.HasValue && majorNames.TryGetValue(course.MajorId.Value, out var name))
        {
            dto.MajorName = name;
        }
        return dto;
    }

    private async Task<Dictionary<Guid, string>> ResolveMajorNamesAsync(IEnumerable<Guid?> majorIds)
    {
        var ids = majorIds
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        var query = await _majorRepository.GetQueryableAsync();
        return await query
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    private async Task<string?> ResolveMajorNameAsync(Guid? majorId)
    {
        if (!majorId.HasValue)
        {
            return null;
        }
        var major = await _majorRepository.FindAsync(majorId.Value);
        return major?.Name;
    }
}
