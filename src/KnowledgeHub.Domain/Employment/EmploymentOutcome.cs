using System;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class EmploymentOutcome : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public EmploymentOutcomeStatus Status { get; set; } = EmploymentOutcomeStatus.Intention;
    public string? EmploymentType { get; set; }
    public string? Region { get; set; }
    public string? SalaryRange { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
    public bool IsPrimary { get; set; } = true;

    public EmploymentOutcome()
    {
    }

    public EmploymentOutcome(Guid id, Guid studentId, string employerName, string jobTitle)
        : base(id)
    {
        StudentId = studentId;
        EmployerName = employerName;
        JobTitle = jobTitle;
    }
}
