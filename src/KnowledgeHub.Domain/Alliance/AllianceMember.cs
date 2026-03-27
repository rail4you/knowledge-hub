using System;
using KnowledgeHub.Alliance.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Alliance;

public class AllianceMember : Entity<Guid>, IMultiTenant
{
    public Guid AllianceId { get; set; }
    public Guid MemberTenantId { get; set; }
    public string TenantName { get; set; }
    public AllianceMemberRole Role { get; set; }
    public DateTime JoinedTime { get; set; }
    public Guid? TenantId { get; set; }

    public AllianceMember()
    {
    }

    public AllianceMember(Guid allianceId, Guid memberTenantId, string tenantName, AllianceMemberRole role)
    {
        AllianceId = allianceId;
        MemberTenantId = memberTenantId;
        TenantName = tenantName;
        Role = role;
        JoinedTime = DateTime.UtcNow;
    }
}
