using System;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class JobApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ResumeId { get; set; }

    /// <summary>
    /// 投递时的简历快照（JSON，V2.0）。学生后续修改简历不影响已投递材料；
    /// 为空表示老数据（当时未做快照），读取时回退到实时简历。
    /// </summary>
    public string? ResumeSnapshotJson { get; set; }

    /// <summary>投递时的简历版本号（快照标识）。</summary>
    public int? ResumeVersionId { get; set; }

    /// <summary>快照时间。</summary>
    public DateTime? SnapshotAt { get; set; }
    public string? CoverLetter { get; set; }
    public EmploymentApplicationStatus Status { get; set; } = EmploymentApplicationStatus.Submitted;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? EmployerRemark { get; set; }

    public JobApplication()
    {
    }

    public JobApplication(Guid id, Guid jobPostingId, Guid studentId, Guid resumeId)
        : base(id)
    {
        JobPostingId = jobPostingId;
        StudentId = studentId;
        ResumeId = resumeId;
    }
}
