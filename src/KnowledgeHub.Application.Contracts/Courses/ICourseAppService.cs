using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses;

public interface ICourseAppService : ICrudAppService<
    CourseDto, 
    Guid, 
    PagedCourseRequestDto, 
    CreateUpdateCourseDto>
{
    Task<PagedResultDto<CourseDto>> GetPublishedAsync(PagedCourseRequestDto input);
    Task<CourseDetailDto> GetDetailAsync(Guid id);
    /// <summary>发布课程：草稿 → 已发布。发布后学生端才可见、才可选课。</summary>
    Task<CourseDto> PublishAsync(Guid id);
    /// <summary>撤回发布：已发布 → 草稿。撤回后学生端不再展示、不可再选课。</summary>
    Task<CourseDto> UnpublishAsync(Guid id);
    Task EnrollAsync(Guid courseId);
    Task DropAsync(Guid courseId);
    Task<PagedResultDto<CourseDto>> GetMyCoursesAsync(PagedCourseRequestDto input);
    Task<AuditResultDto> AuditAsync(Guid courseId, AuditCourseDto input);
    Task<PagedResultDto<CourseDto>> GetByFilterAsync(CourseFilterDto filter);
    Task<List<string>> GetSemestersAsync();
}
