using System;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;

namespace KnowledgeHub.Courses;

public interface IStudentCourseAppService : IApplicationService
{
    Task<PagedResultDto<StudentCourseDto>> GetPagedAsync(GetStudentCoursesInput input);
    Task<PagedResultDto<IdentityUserDto>> GetAvailableStudentsAsync(GetAvailableStudentsInput input);
    Task EnrollStudentAsync(CreateStudentCourseDto input);
    Task BatchEnrollAsync(BatchEnrollDto input);
    Task DeleteAsync(Guid id);
}
