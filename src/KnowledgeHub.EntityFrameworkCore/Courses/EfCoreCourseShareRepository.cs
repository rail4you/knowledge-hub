using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.EntityFrameworkCore.Courses;

public class EfCoreCourseShareRepository : EfCoreRepository<KnowledgeHubDbContext, CourseShare, Guid>, ICourseShareRepository
{
    private readonly IDataFilter _dataFilter;

    public EfCoreCourseShareRepository(
        IDbContextProvider<KnowledgeHubDbContext> dbContextProvider,
        IDataFilter dataFilter)
        : base(dbContextProvider)
    {
        _dataFilter = dataFilter;
    }

    public async Task<List<CourseShare>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(x => x.CourseId == courseId)
            .OrderByDescending(x => x.SharedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询共享给某个目标租户的所有课程共享记录。
    /// 记录按源租户隔离存储，跨租户的目标租户查询必须禁用租户过滤器。
    /// </summary>
    public async Task<List<CourseShare>> GetByTargetTenantAsync(Guid targetTenantId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await GetQueryableAsync();
            return await query
                .Where(x => x.TargetTenantId == targetTenantId)
                .OrderByDescending(x => x.SharedAt)
                .ToListAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid courseId, Guid targetTenantId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await GetQueryableAsync();
            return await query.AnyAsync(
                x => x.CourseId == courseId && x.TargetTenantId == targetTenantId,
                cancellationToken);
        }
    }
}
