using System;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Alliance;

public class AllianceAudit : AuditedEntity<Guid>, IMultiTenant
{
    public Guid? AllianceId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid ApproverTenantId { get; set; }
    public string ApproverTenantName { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
    public Guid? TenantId { get; set; }

    public AllianceAudit()
    {
    }

    public AllianceAudit(Guid? allianceId, Guid resourceId, Guid approverTenantId, string approverTenantName, AuditStatus status, string? comment = null)
    {
        AllianceId = allianceId;
        ResourceId = resourceId;
        ApproverTenantId = approverTenantId;
        ApproverTenantName = approverTenantName;
        Status = status;
        Comment = comment;
    }
}
