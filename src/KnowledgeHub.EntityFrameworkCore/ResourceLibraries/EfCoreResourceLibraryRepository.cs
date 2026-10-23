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

    public EfCoreResourceLibraryRepository(
        IDbContextProvider<KnowledgeHubDbContext> dbContextProvider,
        IRepository<ResourceLibraryMember, Guid> memberRepository,
        IDataFilter dataFilter)
        : base(dbContextProvider)
    {
        _memberRepository = memberRepository;
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
}
