using System;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses;

public interface ICourseLessonPlanAppService : IApplicationService
{
    Task<PagedResultDto<CourseLessonPlanDto>> GetListAsync(CourseLessonPlanQueryDto input);

    Task<CourseLessonPlanDto> GetAsync(Guid id);

    Task<CourseLessonPlanDto> CreateAsync(CreateCourseLessonPlanDto input);

    Task<CourseLessonPlanDto> UpdateAsync(Guid id, UpdateCourseLessonPlanDto input);

    Task DeleteAsync(Guid id);
}
