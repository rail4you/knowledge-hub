using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace KnowledgeHub.Courses;

public class CourseAppService : ApplicationService, ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CourseAppService> _logger;

    public CourseAppService(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        ICurrentUser currentUser,
        ILogger<CourseAppService> logger)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _studentCourseRepository = studentCourseRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Create)]
    public async Task<CourseDto> CreateAsync(CreateUpdateCourseDto input)
    {
        var course = new Course(GuidGenerator.Create(), input.Title);
        course.Description = input.Description;
        course.CoverImageUrl = input.CoverImageUrl;
        course.Major = input.Major;
        course.Semester = input.Semester;
        course.Credits = input.Credits;
        course.SemesterHours = input.SemesterHours;
        course.Difficulty = input.Difficulty;
        course.CategoryId = input.CategoryId;
        course.Status = input.Status;
        course.TeacherId = _currentUser.Id;

        await _courseRepository.InsertAsync(course);
        
        return MapToDto(course);
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
        course.Major = input.Major;
        course.Semester = input.Semester;
        course.Credits = input.Credits;
        course.SemesterHours = input.SemesterHours;
        course.Difficulty = input.Difficulty;
        course.CategoryId = input.CategoryId;
        course.Status = input.Status;

        await _courseRepository.UpdateAsync(course);

        return MapToDto(course);
    }

    public async Task<CourseDto> GetAsync(Guid id)
    {
        var course = await _courseRepository.FindAsync(id);
        if (course == null)
        {
            return null;
        }
        return MapToDto(course);
    }

    public async Task<PagedResultDto<CourseDto>> GetListAsync(PagedCourseRequestDto input)
    {
        var query = await _courseRepository.GetQueryableAsync();
        query = query.WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Major), x => x.Major == input.Major)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                     .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                     .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
                     .WhereIf(input.Status.HasValue, x => x.Status == input.Status);

        var totalCount = query.Count();
        var courses = query.OrderByDescending(x => x.CreationTime)
                           .Skip(input.SkipCount)
                           .Take(input.MaxResultCount)
                           .ToList();

        return new PagedResultDto<CourseDto>(
            totalCount,
            courses.Select(MapToDto).ToList()
        );
    }

    [Authorize(KnowledgeHubPermissions.Courses.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _courseRepository.DeleteAsync(id);
    }

    public async Task<CourseDetailDto> GetDetailAsync(Guid id)
    {
        var course = await _courseRepository.FindAsync(id);
        if (course == null)
        {
            return null;
        }
        
        var chapters = await _chapterRepository.GetListAsync(x => x.CourseId == id);
        var knowledgeResources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == id);
        
        return new CourseDetailDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CoverImageUrl = course.CoverImageUrl,
            Major = course.Major,
            Semester = course.Semester,
            Credits = course.Credits,
            SemesterHours = course.SemesterHours,
            Difficulty = course.Difficulty,
            Status = course.Status,
            TeacherId = course.TeacherId,
            CategoryId = course.CategoryId,
            Chapters = chapters.OrderBy(c => c.SortOrder).Select(ch => new ChapterDto
            {
                Id = ch.Id,
                CourseId = ch.CourseId,
                Title = ch.Title,
                Description = ch.Description,
                SortOrder = ch.SortOrder
            }).ToList()
        };
    }

    public async Task<PagedResultDto<CourseDto>> GetPublishedAsync(PagedCourseRequestDto input)
    {
        var query = await _courseRepository.GetQueryableAsync();
        query = query.Where(x => x.Status == CourseStatus.Published)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title.Contains(input.Filter))
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Major), x => x.Major == input.Major)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                     .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                     .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId);

        var totalCount = query.Count();
        var courses = query.OrderByDescending(x => x.CreationTime)
                           .Skip(input.SkipCount)
                           .Take(input.MaxResultCount)
                           .ToList();

        return new PagedResultDto<CourseDto>(
            totalCount,
            courses.Select(c => new CourseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CoverImageUrl = c.CoverImageUrl,
                Major = c.Major,
                Semester = c.Semester,
                Credits = c.Credits,
                SemesterHours = c.SemesterHours,
                Status = c.Status,
                Difficulty = c.Difficulty,
                TeacherId = c.TeacherId,
                CategoryId = c.CategoryId,
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
                                  .WhereIf(!string.IsNullOrWhiteSpace(input.Major), x => x.Major == input.Major)
                                  .WhereIf(!string.IsNullOrWhiteSpace(input.Semester), x => x.Semester == input.Semester)
                                  .WhereIf(input.Difficulty.HasValue, x => x.Difficulty == input.Difficulty)
                                  .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId)
                                  .ToList();

        return new PagedResultDto<CourseDto>(
            courses.Count,
            courses.Select(MapToDto).ToList()
        );
    }

    [Authorize]
    public async Task<PagedResultDto<CourseDto>> GetByFilterAsync(CourseFilterDto filter)
    {
        var query = await _courseRepository.GetQueryableAsync();
        query = query.WhereIf(!string.IsNullOrWhiteSpace(filter.Filter), x => x.Title.Contains(filter.Filter))
                     .WhereIf(!string.IsNullOrWhiteSpace(filter.Major), x => x.Major == filter.Major)
                     .WhereIf(!string.IsNullOrWhiteSpace(filter.Semester), x => x.Semester == filter.Semester)
                     .WhereIf(filter.Difficulty.HasValue, x => x.Difficulty == filter.Difficulty)
                     .WhereIf(filter.CategoryId.HasValue, x => x.CategoryId == filter.CategoryId)
                     .WhereIf(filter.TeacherId.HasValue, x => x.TeacherId == filter.TeacherId)
                     .WhereIf(filter.Status.HasValue, x => x.Status == filter.Status);

        var courses = query.OrderByDescending(x => x.CreationTime).ToList();

        return new PagedResultDto<CourseDto>(
            courses.Count,
            courses.Select(MapToDto).ToList()
        );
    }

    public async Task<List<string>> GetMajorsAsync()
    {
        var query = await _courseRepository.GetQueryableAsync();
        return query.Where(x => x.Major != null)
                    .Select(x => x.Major!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
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

    private CourseDto MapToDto(Course course)
    {
        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CoverImageUrl = course.CoverImageUrl,
            Major = course.Major,
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
}
