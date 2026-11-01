using System;
using System.Collections.Generic;
using KnowledgeHub.Cooperation;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Cooperation.Dtos;

public class CooperationDemandDto
{
    public Guid Id { get; set; }
    public Guid EnterpriseProfileId { get; set; }
    public string? EnterpriseName { get; set; }
    public CooperationDemandType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Majors { get; set; }
    public string? Industries { get; set; }
    public string? Region { get; set; }
    public string? Background { get; set; }
    public string? EnterpriseInput { get; set; }
    public string? SchoolConditions { get; set; }
    public string? Cycle { get; set; }
    public string? Scale { get; set; }
    public string? BudgetNote { get; set; }
    public string? DeliveryAcceptance { get; set; }
    public string? IpTerms { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public CooperationDemandScope Scope { get; set; }
    public CooperationDemandStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string? RejectReason { get; set; }
    public int ResponseCount { get; set; }
    public DateTime CreationTime { get; set; }
    public bool CanEdit { get; set; }
}

public class CreateUpdateCooperationDemandDto
{
    public CooperationDemandType Type { get; set; } = CooperationDemandType.TalentTraining;
    public string Title { get; set; } = string.Empty;
    public string? Majors { get; set; }
    public string? Industries { get; set; }
    public string? Region { get; set; }
    public string? Background { get; set; }
    public string? EnterpriseInput { get; set; }
    public string? SchoolConditions { get; set; }
    public string? Cycle { get; set; }
    public string? Scale { get; set; }
    public string? BudgetNote { get; set; }
    public string? DeliveryAcceptance { get; set; }
    public string? IpTerms { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public CooperationDemandScope Scope { get; set; } = CooperationDemandScope.HostSchoolOnly;
}

public class CooperationDemandListRequestDto : PagedResultRequestDto
{
    public CooperationDemandType? Type { get; set; }
    public CooperationDemandStatus? Status { get; set; }
    public string? Region { get; set; }
    public string? Major { get; set; }
    public string? Filter { get; set; }

    /// <summary>只看本企业（企业侧「我的需求」）。</summary>
    public bool OnlyMine { get; set; }
}

public class CooperationResponseDto
{
    public Guid Id { get; set; }
    public Guid DemandId { get; set; }
    public Guid SchoolTenantId { get; set; }
    public string? SchoolTenantName { get; set; }
    public Guid ResponderUserId { get; set; }
    public string? ResponderName { get; set; }
    public string? Note { get; set; }
    public CooperationResponseStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreationTime { get; set; }
    public bool CanDecide { get; set; }
}

public class CreateCooperationResponseDto
{
    public Guid DemandId { get; set; }
    public string? Note { get; set; }
}

public class DecideCooperationResponseDto
{
    public bool Accept { get; set; }
    public string? Note { get; set; }
}

public class CooperationProjectDto
{
    public Guid Id { get; set; }
    public Guid? DemandId { get; set; }
    public Guid? ResponseId { get; set; }
    public Guid EnterpriseProfileId { get; set; }
    public string? EnterpriseName { get; set; }
    public Guid SchoolTenantId { get; set; }
    public string? SchoolTenantName { get; set; }
    public string Name { get; set; } = string.Empty;
    public CooperationProjectStage Stage { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string? AgreementRef { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? AcceptanceNote { get; set; }
    public int MilestoneCount { get; set; }
    public int CompletedMilestoneCount { get; set; }
    public List<ProjectMilestoneDto> Milestones { get; set; } = new();
}

public class ProjectMilestoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? EvidenceRef { get; set; }
    public string? Note { get; set; }
    public bool IsCompleted { get; set; }
}

public class CreateCooperationProjectDto
{
    public Guid? ResponseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AgreementRef { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}

public class CreateMilestoneDto
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public int SortOrder { get; set; }
}

public class CompleteMilestoneDto
{
    public Guid MilestoneId { get; set; }
    public string? EvidenceRef { get; set; }
    public string? Note { get; set; }
}

public class AdvanceProjectStageDto
{
    public Guid ProjectId { get; set; }
    public CooperationProjectStage Stage { get; set; }
    public string? AcceptanceNote { get; set; }
}
