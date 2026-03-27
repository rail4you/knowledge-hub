using System;
using System.Collections.Generic;
using KnowledgeHub.Alliance.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Alliance;

public class Alliance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public AllianceStatus Status { get; set; }
    public Guid? TenantId { get; set; }

    public ICollection<AllianceMember> Members { get; set; }
    public ICollection<AllianceAudit> Audits { get; set; }

    public Alliance()
    {
        Members = new List<AllianceMember>();
        Audits = new List<AllianceAudit>();
    }
}
