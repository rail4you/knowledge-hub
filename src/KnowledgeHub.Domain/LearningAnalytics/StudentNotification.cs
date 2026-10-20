using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>站内消息（学生端「消息」Tab：薄弱点报告、开课前衔接提醒等）。</summary>
public class StudentNotification : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudentId { get; set; }

    public AnalysisNotificationType Type { get; set; } = AnalysisNotificationType.Custom;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>来源展示文案（「系统」「李老师」等）</summary>
    public string Source { get; set; } = "系统";

    public Guid? CourseId { get; set; }

    public Guid? RelatedId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    private StudentNotification() { }

    public StudentNotification(Guid id, Guid studentId, string title, string content) : base(id)
    {
        StudentId = studentId;
        Title = title;
        Content = content;
    }

    public void MarkRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
