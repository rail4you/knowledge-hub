using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Courses;

public interface ICourseShareRepository : IRepository<CourseShare, Guid>
{
    /// <summary>获取某课程被共享给了哪些目标租户（源租户视角，租户过滤器自动隔离）。</summary>
    Task<List<CourseShare>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>获取共享给指定目标租户的全部记录（忽略 ABP 租户过滤器，按 TargetTenantId 精确匹配）。</summary>
    Task<List<CourseShare>> GetByTargetTenantAsync(Guid targetTenantId, CancellationToken cancellationToken = default);

    /// <summary>判断课程是否已共享给指定目标租户。</summary>
    Task<bool> ExistsAsync(Guid courseId, Guid targetTenantId, CancellationToken cancellationToken = default);
}
