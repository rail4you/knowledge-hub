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

    // ── V2.0 去向核实（学校审核后才进正式统计） ──

    /// <summary>证明材料（三方协议/劳动合同附件引用）。</summary>
    public string? EvidenceUrl { get; set; }

    /// <summary>是否已核实（学校审核通过）。</summary>
    public bool IsVerified { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public Guid? VerifiedByUserId { get; set; }

    public string? VerifyNote { get; set; }

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
