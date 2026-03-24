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
    Task EnrollAsync(Guid courseId);
    Task DropAsync(Guid courseId);
    Task<PagedResultDto<CourseDto>> GetMyCoursesAsync(PagedCourseRequestDto input);
    Task<AuditResultDto> AuditAsync(Guid courseId, AuditCourseDto input);
    Task<PagedResultDto<CourseDto>> GetByFilterAsync(CourseFilterDto filter);
    Task<List<string>> GetMajorsAsync();
    Task<List<string>> GetSemestersAsync();
}
