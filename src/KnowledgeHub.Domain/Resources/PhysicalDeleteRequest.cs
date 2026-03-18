using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Resources;

public enum PhysicalDeleteStatus
{
    Pending,
    Approved,
    Rejected
}

public class PhysicalDeleteRequest : AuditedEntity<Guid>
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; }
    public string Reason { get; set; }
    public PhysicalDeleteStatus Status { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ApprovalTime { get; set; }
}
