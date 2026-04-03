using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Alliance;
using KnowledgeHub.Edition;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;
using AllianceEntity = KnowledgeHub.Alliance.Alliance;
using AllianceMemberEntity = KnowledgeHub.Alliance.AllianceMember;
using AllianceAuditEntity = KnowledgeHub.Alliance.AllianceAudit;
using AllianceEnums = KnowledgeHub.Alliance.Enums;
using AllianceMemberRole = KnowledgeHub.Alliance.Enums.AllianceMemberRole;
using AllianceStatus = KnowledgeHub.Alliance.Enums.AllianceStatus;
using AllianceRepo = KnowledgeHub.Alliance.IAllianceRepository;
using AllianceMemberRepo = KnowledgeHub.Alliance.IAllianceMemberRepository;
using AllianceAuditRepo = KnowledgeHub.Alliance.IAllianceAuditRepository;
using ResourceEntity = KnowledgeHub.Resources.Resource;
using ResourceStatus = KnowledgeHub.Resources.Enums.ResourceStatus;
using AuditStatus = KnowledgeHub.Resources.Enums.AuditStatus;

namespace KnowledgeHub.Application.Alliance;

[Authorize(KnowledgeHubPermissions.Alliance.Default)]
public class AllianceAppService : KnowledgeHubAppService, IAllianceAppService
{
    protected IRepository<AllianceEntity, Guid> AllianceRepository { get; }
    protected AllianceMemberRepo MemberRepository { get; }
    protected AllianceAuditRepo AuditRepository { get; }
    protected IRepository<ResourceEntity, Guid> ResourceRepository { get; }
    protected IEditionConfigService EditionConfigService { get; }
    protected ICurrentTenant CurrentTenant { get; }

    public AllianceAppService(
        IRepository<AllianceEntity, Guid> allianceRepository,
        AllianceMemberRepo memberRepository,
        AllianceAuditRepo auditRepository,
        IRepository<ResourceEntity, Guid> resourceRepository,
        IEditionConfigService editionConfigService,
        ICurrentTenant currentTenant)
    {
        AllianceRepository = allianceRepository;
        MemberRepository = memberRepository;
        AuditRepository = auditRepository;
        ResourceRepository = resourceRepository;
        EditionConfigService = editionConfigService;
        CurrentTenant = currentTenant;
    }

    public virtual async Task<AllianceDto> GetAsync(Guid id)
    {
        var alliance = await AllianceRepository.GetAsync(id);
        return ObjectMapper.Map<AllianceEntity, AllianceDto>(alliance);
    }

    public virtual async Task<PagedResultDto<AllianceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await AllianceRepository.GetQueryableAsync();
        var totalCount = query.Count();
        var alliances = query
            .OrderByDescending(a => a.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = new List<AllianceDto>();
        foreach (var alliance in alliances)
        {
            var dto = ObjectMapper.Map<AllianceEntity, AllianceDto>(alliance);
            dto.MemberCount = (await MemberRepository.GetByAllianceIdAsync(alliance.Id)).Count;
            dtos.Add(dto);
        }

        return new PagedResultDto<AllianceDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Alliance.Create)]
    public virtual async Task<AllianceDto> CreateAsync(CreateAllianceDto input)
    {
        var isAllianceEnabled = await EditionConfigService.IsAllianceEnabledAsync();
        if (!isAllianceEnabled)
        {
            throw new UserFriendlyException("联盟功能未启用，请升级到标准版");
        }

        var alliance = new AllianceEntity
        {
            Name = input.Name,
            Description = input.Description,
            Status = AllianceStatus.Active,
            TenantId = CurrentTenant.Id
        };

        await AllianceRepository.InsertAsync(alliance);
        return ObjectMapper.Map<AllianceEntity, AllianceDto>(alliance);
    }

    [Authorize(KnowledgeHubPermissions.Alliance.Update)]
    public virtual async Task<AllianceDto> UpdateAsync(Guid id, CreateAllianceDto input)
    {
        var alliance = await AllianceRepository.GetAsync(id);
        alliance.Name = input.Name;
        alliance.Description = input.Description;
        await AllianceRepository.UpdateAsync(alliance);
        return ObjectMapper.Map<AllianceEntity, AllianceDto>(alliance);
    }

    [Authorize(KnowledgeHubPermissions.Alliance.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await AllianceRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Alliance.ManageMembers)]
    public virtual async Task<AllianceMemberDto> AddMemberAsync(CreateAllianceMemberDto input)
    {
        var existing = await MemberRepository.GetByTenantIdAsync(input.TenantId);
        if (existing != null)
        {
            throw new UserFriendlyException("该租户已是联盟成员");
        }

        var member = new AllianceMemberEntity(
            input.AllianceId,
            input.TenantId,
            input.TenantName,
            input.Role
        )
        {
            TenantId = CurrentTenant.Id
        };

        await MemberRepository.InsertAsync(member);
        return ObjectMapper.Map<AllianceMemberEntity, AllianceMemberDto>(member);
    }

    [Authorize(KnowledgeHubPermissions.Alliance.ManageMembers)]
    public virtual async Task RemoveMemberAsync(Guid memberId)
    {
        await MemberRepository.DeleteAsync(memberId);
    }

    public virtual async Task<PagedResultDto<AllianceMemberDto>> GetMembersAsync(AllianceMemberQueryDto input)
    {
        List<AllianceMemberEntity> members;
        long totalCount;

        if (input.AllianceId.HasValue)
        {
            members = await MemberRepository.GetByAllianceIdAsync(input.AllianceId.Value);
            totalCount = members.Count;
        }
        else
        {
            var query = await MemberRepository.GetQueryableAsync();
            members = query.Where(m => m.TenantId == CurrentTenant.Id).ToList();
            totalCount = members.Count;
        }

        return new PagedResultDto<AllianceMemberDto>(
            totalCount,
            ObjectMapper.Map<List<AllianceMemberEntity>, List<AllianceMemberDto>>(members)
        );
    }

    [Authorize(KnowledgeHubPermissions.Alliance.ManageMembers)]
    public virtual async Task<AllianceMemberDto> UpdateMemberRoleAsync(Guid memberId, AllianceMemberRole role)
    {
        var member = await MemberRepository.GetAsync(memberId);
        member.Role = role;
        await MemberRepository.UpdateAsync(member);
        return ObjectMapper.Map<AllianceMemberEntity, AllianceMemberDto>(member);
    }

    [Authorize(KnowledgeHubPermissions.Resources.LeagueAudit)]
    public virtual async Task<AllianceAuditDto> LeagueAuditAsync(AllianceAuditInputDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? Guid.Empty;
        var membership = await MemberRepository.GetByTenantIdAsync(currentTenantId);

        var resource = await ResourceRepository.GetAsync(input.ResourceId);
        if (resource.Status != ResourceStatus.SchoolApproved)
        {
            throw new UserFriendlyException("只有学校已批准的资源才能进行联盟审核");
        }

        var allianceId = membership?.AllianceId;
        var tenantName = membership?.TenantName ?? "Host Admin";

        var audit = new AllianceAuditEntity(
            allianceId,
            input.ResourceId,
            currentTenantId,
            tenantName,
            input.Status,
            input.Comment
        )
        {
            TenantId = CurrentTenant.Id
        };

        await AuditRepository.InsertAsync(audit);

        if (input.Status == AuditStatus.Approved)
        {
            resource.Status = ResourceStatus.LeagueApproved;
            await ResourceRepository.UpdateAsync(resource);
        }
        else if (input.Status == AuditStatus.Rejected)
        {
            resource.Status = ResourceStatus.Rejected;
            await ResourceRepository.UpdateAsync(resource);
        }

        var dto = ObjectMapper.Map<AllianceAuditEntity, AllianceAuditDto>(audit);
        dto.ResourceName = resource.Name;
        return dto;
    }

    public virtual async Task<PagedResultDto<PendingAllianceAuditDto>> GetPendingAuditsAsync(Guid allianceId, PagedResultRequestDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? Guid.Empty;
        var membership = await MemberRepository.GetByTenantIdAsync(currentTenantId);
        if (membership == null || membership.Role != AllianceMemberRole.Admin)
        {
            return new PagedResultDto<PendingAllianceAuditDto>(0, new List<PendingAllianceAuditDto>());
        }

        var resourceQuery = await ResourceRepository.GetQueryableAsync();
        var pendingResources = resourceQuery
            .Where(r => r.Status == ResourceStatus.SchoolApproved)
            .Where(r => r.TenantId != currentTenantId)
            .OrderByDescending(r => r.CreationTime)
            .ToList();

        var memberTenants = await MemberRepository.GetByAllianceIdAsync(allianceId);
        var memberTenantIds = memberTenants.Select(m => m.MemberTenantId).ToHashSet();

        var alliancePending = pendingResources
            .Where(r => r.TenantId.HasValue && memberTenantIds.Contains(r.TenantId.Value))
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = new List<PendingAllianceAuditDto>();
        foreach (var resource in alliancePending)
        {
            var submitterMembership = memberTenants.FirstOrDefault(m => m.MemberTenantId == resource.TenantId);
            dtos.Add(new PendingAllianceAuditDto
            {
                ResourceId = resource.Id,
                ResourceName = resource.Name,
                SubmitterTenantId = resource.TenantId ?? Guid.Empty,
                SubmitterTenantName = submitterMembership?.TenantName ?? "未知",
                SubmittedTime = resource.CreationTime
            });
        }

        return new PagedResultDto<PendingAllianceAuditDto>(dtos.Count, dtos);
    }

    public virtual async Task<List<AllianceAuditDto>> GetResourceAuditsAsync(Guid resourceId)
    {
        var audits = await AuditRepository.GetByResourceIdAsync(resourceId);
        var dtos = ObjectMapper.Map<List<AllianceAuditEntity>, List<AllianceAuditDto>>(audits);

        var resourceQuery = await ResourceRepository.GetQueryableAsync();
        foreach (var dto in dtos)
        {
            var resource = resourceQuery.FirstOrDefault(r => r.Id == dto.ResourceId);
            dto.ResourceName = resource?.Name ?? "未知";
        }

        return dtos;
    }

    public virtual async Task<bool> IsAllianceEnabledAsync()
    {
        return await EditionConfigService.IsAllianceEnabledAsync();
    }

    public virtual async Task<bool> IsTenantInAllianceAsync(Guid tenantId)
    {
        return await MemberRepository.IsMemberOfAnyAllianceAsync(tenantId);
    }

    public virtual async Task<AllianceMemberDto> GetTenantAllianceMembershipAsync(Guid tenantId)
    {
        var member = await MemberRepository.GetByTenantIdAsync(tenantId);
        if (member == null)
        {
            throw new UserFriendlyException("该租户不是联盟成员");
        }
        return ObjectMapper.Map<AllianceMemberEntity, AllianceMemberDto>(member);
    }
}
