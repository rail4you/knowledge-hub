using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using AllianceEntity = KnowledgeHub.Alliance.Alliance;
using AllianceMemberEntity = KnowledgeHub.Alliance.AllianceMember;
using AllianceAuditEntity = KnowledgeHub.Alliance.AllianceAudit;
using AllianceRepo = KnowledgeHub.Alliance.IAllianceRepository;
using AllianceMemberRepo = KnowledgeHub.Alliance.IAllianceMemberRepository;
using AllianceAuditRepo = KnowledgeHub.Alliance.IAllianceAuditRepository;
using ResourceEnums = KnowledgeHub.Resources.Enums;

namespace KnowledgeHub.EntityFrameworkCore.Alliance;

public class EfCoreAllianceRepository : EfCoreRepository<KnowledgeHubDbContext, AllianceEntity, Guid>,
    AllianceRepo
{
    public EfCoreAllianceRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<AllianceEntity> GetWithMembersAsync(Guid id)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Alliances
            .Include(a => a.Members)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<AllianceEntity>> GetListAsync(int skipCount, int maxResultCount)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Alliances
            .OrderByDescending(a => a.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync();
    }

    public async Task<long> GetCountAsync()
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Alliances.CountAsync();
    }
}

public class EfCoreAllianceMemberRepository : EfCoreRepository<KnowledgeHubDbContext, AllianceMemberEntity, Guid>,
    AllianceMemberRepo
{
    public EfCoreAllianceMemberRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<AllianceMemberEntity>> GetByAllianceIdAsync(Guid allianceId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.AllianceMembers
            .Where(m => m.AllianceId == allianceId)
            .ToListAsync();
    }

    public async Task<AllianceMemberEntity?> GetByTenantIdAsync(Guid tenantId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.AllianceMembers
            .FirstOrDefaultAsync(m => m.MemberTenantId == tenantId);
    }

    public async Task<bool> IsMemberOfAnyAllianceAsync(Guid tenantId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.AllianceMembers
            .AnyAsync(m => m.MemberTenantId == tenantId);
    }

    public async Task<List<AllianceMemberEntity>> GetByTenantIdListAsync(List<Guid> tenantIds)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.AllianceMembers
            .Where(m => tenantIds.Contains(m.MemberTenantId))
            .ToListAsync();
    }
}

public class EfCoreAllianceAuditRepository : EfCoreRepository<KnowledgeHubDbContext, AllianceAuditEntity, Guid>,
    AllianceAuditRepo
{
    public EfCoreAllianceAuditRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<AllianceAuditEntity>> GetByResourceIdAsync(Guid resourceId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.AllianceAudits
            .Where(a => a.ResourceId == resourceId)
            .OrderByDescending(a => a.CreationTime)
            .ToListAsync();
    }

    public async Task<List<AllianceAuditEntity>> GetPendingAuditsAsync(Guid allianceId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.AllianceAudits
            .Where(a => a.AllianceId == allianceId)
            .Where(a => a.Status == ResourceEnums.AuditStatus.Pending)
            .OrderByDescending(a => a.CreationTime)
            .ToListAsync();
    }
}
