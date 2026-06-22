using System;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class InterviewSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? EmployerUserId { get; set; }
    /// <summary>
    /// 面试官用户 ID（P1-8）。可空：未指定面试官时仅保留 InterviewerName。
    /// </summary>
    public Guid? InterviewerId { get; set; }
    public string InterviewerName { get; set; } = string.Empty;
    public string? InterviewerPhone { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Note { get; set; }
    public EmploymentInterviewResult Result { get; set; } = EmploymentInterviewResult.Pending;
    public string? Summary { get; set; }
    public string? ResultComment { get; set; }
    public DateTime? ResultRecordedAt { get; set; }

    public InterviewSchedule()
    {
    }

    public InterviewSchedule(Guid id, Guid applicationId, Guid jobPostingId, Guid studentId, string interviewerName, DateTime scheduledAt)
        : base(id)
    {
        ApplicationId = applicationId;
        JobPostingId = jobPostingId;
        StudentId = studentId;
        InterviewerName = interviewerName;
        ScheduledAt = scheduledAt;
    }
}
