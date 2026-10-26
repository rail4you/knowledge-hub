using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.ResourceLibraries;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.EntityFrameworkCore.ResourceLibraries;

public class EfCoreResourceLibraryRepository
    : EfCoreRepository<KnowledgeHubDbContext, ResourceLibrary, Guid>, IResourceLibraryRepository
{
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<ResourceLibraryMember, Guid> _memberRepository;
    private readonly IRepository<ResourceLibraryTeacher, Guid> _teacherRepository;

    public EfCoreResourceLibraryRepository(
        IDbContextProvider<KnowledgeHubDbContext> dbContextProvider,
        IRepository<ResourceLibraryMember, Guid> memberRepository,
        IRepository<ResourceLibraryTeacher, Guid> teacherRepository,
        IDataFilter dataFilter)
        : base(dbContextProvider)
    {
        _memberRepository = memberRepository;
        _teacherRepository = teacherRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// 成员记录按「库归属租户」隔离，但成员学校可能是合作学校（跨租户），
    /// 因此查询成员必须禁用 ABP 租户过滤器后再按 LibraryId 精确匹配。
    /// </summary>
    public async Task<List<ResourceLibraryMember>> GetMembersAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            var members = await query
                .Where(x => x.LibraryId == libraryId)
                .OrderByDescending(x => x.Role)
                .ThenBy(x => x.JoinedTime)
                .ToListAsync(cancellationToken);

            return members;
        }
    }

    /// <summary>
    /// 取得某学校参与共建的全部资源库（含作为主建学校与作为合作学校的）。
    /// 需要跨租户读取：库归属主建学校，合作学校在自身上下文看不到。
    /// </summary>
    public async Task<List<ResourceLibrary>> GetByMemberTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await GetQueryableAsync();
            var memberRows = await query
                .Where(x => x.Members.Any(m => m.MemberTenantId == tenantId))
                .ToListAsync(cancellationToken);

            return memberRows;
        }
    }

    public async Task<bool> IsMemberAsync(Guid libraryId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await GetQueryableAsync();
            return await query.AnyAsync(
                x => x.Id == libraryId && x.Members.Any(m => m.MemberTenantId == tenantId),
                cancellationToken);
        }
    }

    /// <summary>
    /// 分配记录按「教师所属租户」隔离，但共建库中教师可能来自关联学校，
    /// 因此查询分配记录必须禁用 ABP 租户过滤器后再按 LibraryId 精确匹配。
    /// </summary>
    public async Task<List<ResourceLibraryTeacher>> GetTeachersAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _teacherRepository.GetQueryableAsync();
            var teachers = await query
                .Where(x => x.LibraryId == libraryId)
                .OrderBy(x => x.UserName)
                .ToListAsync(cancellationToken);

            return teachers;
        }
    }

    /// <summary>
    /// 取教师被分配到的全部库 Id：教师可能在共建库里被别校管理员分配，
    /// 分配记录落在教师自己租户下，但库可能归属别的租户 —— 查询本身不跨租户，
    /// 返回 Id 后再由上层做跨租户库解析。
    /// </summary>
    public async Task<List<Guid>> GetLibraryIdsByTeacherAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var query = await _teacherRepository.GetQueryableAsync();
        var ids = await query
            .Where(x => x.UserId == userId)
            .Select(x => x.LibraryId)
            .ToListAsync(cancellationToken);

        return ids;
    }

    public async Task<bool> IsTeacherAssignedAsync(Guid libraryId, Guid userId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _teacherRepository.GetQueryableAsync();
            return await query.AnyAsync(
                x => x.LibraryId == libraryId && x.UserId == userId,
                cancellationToken);
        }
    }

    /// <summary>
    /// 取某学校的候选资源库（主建的 + 参与共建的）。
    /// 需要跨租户读取：库归属主建学校，关联学校在自身上下文看不到。
    ///
    /// <para>
    /// 必须 Include 共建成员：上层要靠 <c>Members</c> 判断「本租户是否为关联学校」，
    /// 而 ABP 未启用懒加载代理，不预加载的话该集合会是空的。
    /// </para>
    /// </summary>
    public async Task<List<ResourceLibrary>> GetByTenantOrMemberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await GetQueryableAsync();
            var libraries = await query
                .Include(x => x.Members)
                .Where(x => x.OwnerTenantId == tenantId ||
                            x.Members.Any(m => m.MemberTenantId == tenantId))
                .ToListAsync(cancellationToken);

            return libraries;
        }
    }

    /// <summary>
    /// 跨租户取库并预加载共建成员（见 <see cref="GetByTenantOrMemberAsync"/> 的说明）。
    /// </summary>
    public async Task<ResourceLibrary?> FindWithMembersAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await GetQueryableAsync();
            return await query
                .Include(x => x.Members)
                .FirstOrDefaultAsync(x => x.Id == libraryId, cancellationToken);
        }
    }
}
