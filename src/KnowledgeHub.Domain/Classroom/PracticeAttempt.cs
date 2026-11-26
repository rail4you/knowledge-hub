using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>学生对某课堂练习场次的一次提交（(SessionId, StudentId) 唯一，重复提交按最后一次覆盖）。</summary>
public class PracticeAttempt : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid SessionId { get; set; }

    public Guid StudentId { get; set; }

    public string? StudentName { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>客观题答对数。</summary>
    public int CorrectCount { get; set; }

    /// <summary>客观题总数（主观题不计入，单独在答案里标记未判）。</summary>
    public int TotalCount { get; set; }

    private PracticeAttempt() { }

    public PracticeAttempt(Guid id, Guid sessionId, Guid studentId) : base(id)
    {
        SessionId = sessionId;
        StudentId = studentId;
    }
}
