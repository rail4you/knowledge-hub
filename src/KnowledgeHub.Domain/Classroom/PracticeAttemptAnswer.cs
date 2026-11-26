using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>课堂练习的一条作答（客观题即时判定并写回 <see cref="IsCorrect"/>；主观题留空待批）。</summary>
public class PracticeAttemptAnswer : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid AttemptId { get; set; }

    public Guid ExerciseId { get; set; }

    public string? Answer { get; set; }

    /// <summary>是否答对；为 null 表示主观题（未自动判定）。</summary>
    public bool? IsCorrect { get; set; }

    private PracticeAttemptAnswer() { }

    public PracticeAttemptAnswer(Guid id, Guid attemptId, Guid exerciseId) : base(id)
    {
        AttemptId = attemptId;
        ExerciseId = exerciseId;
    }
}
