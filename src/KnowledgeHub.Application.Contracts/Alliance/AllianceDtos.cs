using System;
using KnowledgeHub.Alliance.Enums;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Application.Contracts.Alliance;

public class AllianceDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public AllianceStatus Status { get; set; }
    public int MemberCount { get; set; }
}

public class AllianceMemberDto : EntityDto<Guid>
{
    public Guid AllianceId { get; set; }
    public Guid MemberTenantId { get; set; }
    public string TenantName { get; set; }
    public AllianceMemberRole Role { get; set; }
    public DateTime JoinedTime { get; set; }
}

public class AllianceAuditDto : EntityDto<Guid>
{
    public Guid AllianceId { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; }
    public Guid ApproverTenantId { get; set; }
    public string ApproverTenantName { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
}

public class CreateAllianceDto
{
    public string Name { get; set; }
    public string? Description { get; set; }
}

public class CreateAllianceMemberDto
{
    public Guid AllianceId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; }
    public AllianceMemberRole Role { get; set; }
}

public class AllianceAuditInputDto
{
    public Guid ResourceId { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
}

public class PendingAllianceAuditDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; }
    public Guid SubmitterTenantId { get; set; }
    public string SubmitterTenantName { get; set; }
    public DateTime SubmittedTime { get; set; }
}

public class AllianceMemberQueryDto : PagedAndSortedResultRequestDto
{
    public Guid? AllianceId { get; set; }
}
