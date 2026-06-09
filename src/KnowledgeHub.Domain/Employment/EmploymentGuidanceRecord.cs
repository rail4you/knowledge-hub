using System;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class EmploymentGuidanceRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? ApplicationId { get; set; }
    /// <summary>
    /// 教师 ID。AI 生成的指导记录无教师，可为 null。
    /// </summary>
    public Guid? TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public EmploymentGuidanceSourceType SourceType { get; set; } = EmploymentGuidanceSourceType.Manual;
    public string? CareerGoal { get; set; }
    public DateTime GuidedAt { get; set; } = DateTime.UtcNow;

    public EmploymentGuidanceRecord()
    {
    }

    public EmploymentGuidanceRecord(Guid id, Guid studentId, Guid? teacherId, string title, string content)
        : base(id)
    {
        StudentId = studentId;
        TeacherId = teacherId;
        Title = title;
        Content = content;
    }
}
