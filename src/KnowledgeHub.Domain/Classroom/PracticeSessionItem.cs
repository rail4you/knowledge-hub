using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>场次内的题目快照（(SessionId, ExerciseId) 唯一，SortOrder 决定下发顺序）。</summary>
public class PracticeSessionItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid SessionId { get; set; }

    public Guid ExerciseId { get; set; }

    public int SortOrder { get; set; }

    private PracticeSessionItem() { }

    public PracticeSessionItem(Guid id, Guid sessionId, Guid exerciseId, int sortOrder) : base(id)
    {
        SessionId = sessionId;
        ExerciseId = exerciseId;
        SortOrder = sortOrder;
    }
}
