using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses.Dtos;

public interface ICourseResourceAppService : IApplicationService
{
    /// <summary>获取某课程关联的资源列表</summary>
    Task<List<CourseResourceDto>> GetByCourseAsync(Guid courseId);

    /// <summary>获取某资源被哪些课程关联（一个资源可关联多个课程）</summary>
    Task<List<CourseResourceDto>> GetByResourceAsync(Guid resourceId);

    /// <summary>将资源文件关联到课程</summary>
    Task<CourseResourceDto> CreateAsync(CreateCourseResourceDto input);

    /// <summary>更新课程资源的显示名/排序/推荐开关</summary>
    Task<CourseResourceDto> UpdateAsync(Guid id, UpdateCourseResourceDto input);

    /// <summary>取消资源与课程的关联</summary>
    Task DeleteAsync(Guid id);
}
