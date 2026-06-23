using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Exams;

/// <summary>
/// P2-4：题目（Exercise）与章节（Chapter）多对多关联表。
/// 设计要点：
/// 1) Exercise.ChapterId 仍保留，作为「主章节」（向后兼容，避免破坏老数据）。
///    同一道题如果只属于一个章节，老逻辑仍然走 Exercise.ChapterId，不必 join。
/// 2) ChapterExercise 用于「同一题被复用到多个章节」的场景（教师拖拽 / 共享题库）。
/// 3) SortOrder 用于同章节内题目排序，避免全表 OrderBy 创建时间导致顺序错乱。
/// 4) IMultiTenant：跟随租户隔离，避免跨租户越权。
/// </summary>
public class ChapterExercise : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ChapterId { get; set; }
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }

    private ChapterExercise() { }

    public ChapterExercise(Guid id, Guid chapterId, Guid exerciseId, int sortOrder = 0) : base(id)
    {
        ChapterId = chapterId;
        ExerciseId = exerciseId;
        SortOrder = sortOrder;
    }
}
