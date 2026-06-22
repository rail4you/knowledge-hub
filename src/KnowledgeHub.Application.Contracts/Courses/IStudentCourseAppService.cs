using System;
using System.Collections.Generic;
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
    /// <summary>P1-10：返回当前筛选下所有可加入的学生 ID（无分页），用于「全选匹配结果」</summary>
    Task<List<Guid>> GetAllAvailableStudentIdsAsync(GetAvailableStudentsInput input);
    Task EnrollStudentAsync(CreateStudentCourseDto input);
    Task BatchEnrollAsync(BatchEnrollDto input);
    Task DeleteAsync(Guid id);
}
