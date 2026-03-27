using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Alliance.Enums;
using KnowledgeHub.Application.Contracts.Alliance;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Alliance;

public interface IAllianceAppService : IApplicationService
{
    Task<AllianceDto> GetAsync(Guid id);
    Task<PagedResultDto<AllianceDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AllianceDto> CreateAsync(CreateAllianceDto input);
    Task<AllianceDto> UpdateAsync(Guid id, CreateAllianceDto input);
    Task DeleteAsync(Guid id);

    Task<AllianceMemberDto> AddMemberAsync(CreateAllianceMemberDto input);
    Task RemoveMemberAsync(Guid memberId);
    Task<PagedResultDto<AllianceMemberDto>> GetMembersAsync(AllianceMemberQueryDto input);
    Task<AllianceMemberDto> UpdateMemberRoleAsync(Guid memberId, AllianceMemberRole role);

    Task<AllianceAuditDto> LeagueAuditAsync(AllianceAuditInputDto input);
    Task<PagedResultDto<PendingAllianceAuditDto>> GetPendingAuditsAsync(Guid allianceId, PagedResultRequestDto input);
    Task<List<AllianceAuditDto>> GetResourceAuditsAsync(Guid resourceId);

    Task<bool> IsAllianceEnabledAsync();
    Task<bool> IsTenantInAllianceAsync(Guid tenantId);
    Task<AllianceMemberDto> GetTenantAllianceMembershipAsync(Guid tenantId);
}
