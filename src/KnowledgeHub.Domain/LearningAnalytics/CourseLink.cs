using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 课程前后关系：只表达「前置课程 → 后续课程」的关联，
/// 用于生成学生端学习路径与衔接复习包（不涉及排课时间）。
/// </summary>
public class CourseLink : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid PredecessorCourseId { get; set; }

    public Guid SuccessorCourseId { get; set; }

    public PrerequisiteStatus Status { get; set; } = PrerequisiteStatus.Confirmed;

    private CourseLink() { }

    public CourseLink(Guid id, Guid predecessorCourseId, Guid successorCourseId) : base(id)
    {
        PredecessorCourseId = predecessorCourseId;
        SuccessorCourseId = successorCourseId;
    }
}
