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

    /// <summary>
    /// 公共资源库里的已发布课程（跨租户）。
    ///
    /// <para>
    /// 「公共资源库」= Visibility=Public（公开）且状态有效的教学资源库，是开放共享区：
    /// 课程归属哪所学校都能放进去，所有租户的学生都能看到并学习
    /// （学生端单独一个「公共课程」区域展示，注明来源学校）。
    /// </para>
    /// </summary>
    Task<PagedResultDto<CourseDto>> GetPublicLibraryCoursesAsync(PagedCourseRequestDto input);
    Task<CourseDetailDto> GetDetailAsync(Guid id);
    /// <summary>
    /// 发布课程：草稿 → 已发布。发布后学生端才可见、才可选课。
    /// <paramref name="input"/> 传入 <c>IsShared = true</c> 时按「共享课」发布：
    /// 课程打上共享课标记并共享给平台「社会学校」租户，所有注册用户均可选课学习。
    /// </summary>
    Task<CourseDto> PublishAsync(Guid id, PublishCourseInput input);
    /// <summary>撤回发布：已发布 → 草稿。撤回后学生端不再展示、不可再选课。</summary>
    Task<CourseDto> UnpublishAsync(Guid id);
    Task EnrollAsync(Guid courseId);
    Task DropAsync(Guid courseId);
    Task<PagedResultDto<CourseDto>> GetMyCoursesAsync(PagedCourseRequestDto input);
    Task<AuditResultDto> AuditAsync(Guid courseId, AuditCourseDto input);
    Task<PagedResultDto<CourseDto>> GetByFilterAsync(CourseFilterDto filter);
    Task<List<string>> GetSemestersAsync();
}
