using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Alliance;

public interface IAllianceRepository : IRepository<Alliance, Guid>
{
    Task<Alliance> GetWithMembersAsync(Guid id);
    Task<List<Alliance>> GetListAsync(int skipCount, int maxResultCount);
    Task<long> GetCountAsync();
}

public interface IAllianceMemberRepository : IRepository<AllianceMember, Guid>
{
    Task<List<AllianceMember>> GetByAllianceIdAsync(Guid allianceId);
    Task<AllianceMember?> GetByTenantIdAsync(Guid tenantId);
    Task<bool> IsMemberOfAnyAllianceAsync(Guid tenantId);
    Task<List<AllianceMember>> GetByTenantIdListAsync(List<Guid> tenantIds);
}

public interface IAllianceAuditRepository : IRepository<AllianceAudit, Guid>
{
    Task<List<AllianceAudit>> GetByResourceIdAsync(Guid resourceId);
    Task<List<AllianceAudit>> GetPendingAuditsAsync(Guid allianceId);
}
