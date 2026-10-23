using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程共享 AppService。
///
/// <para>
/// 需求「租户下的课程和资源是其他学校共享的，需要课程和资源有共享到『社会学校』的功能」的课程侧契约。
/// </para>
/// </summary>
public interface ICourseShareAppService
{
    /// <summary>把课程共享给一个或多个目标学校。</summary>
    Task<List<CourseShareDto>> ShareAsync(CreateCourseShareDto input);

    /// <summary>把课程一键共享到平台唯一「社会学校」。</summary>
    Task<CourseShareDto> ShareToPublicSchoolAsync(Guid courseId, string? note);

    /// <summary>取消共享。</summary>
    Task UnshareAsync(Guid courseId, Guid targetTenantId);

    /// <summary>查看某课程的共享情况。</summary>
    Task<List<CourseShareDto>> GetSharesAsync(Guid courseId);

    /// <summary>共享给我的课程列表。</summary>
    Task<PagedResultDto<SharedCourseDto>> GetSharedToMeAsync(SharedCourseListQueryDto input);

    /// <summary>我共享出去的课程列表。</summary>
    Task<PagedResultDto<SharedCourseDto>> GetSharedByMeAsync(SharedCourseListQueryDto input);
}
