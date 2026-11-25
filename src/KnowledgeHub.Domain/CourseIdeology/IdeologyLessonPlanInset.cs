using System;
using KnowledgeHub.CourseIdeology.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 教案里的「思政融入点」：把某个思政案例（或一条自定义思政点）插到某份教案的某个环节。
///
/// <para>用独立关联表而不是给教案加字段：教案模块完全不知道本模块存在，
/// 删除本表 / 本模块不影响教案的增删改查与导出。</para>
/// </summary>
public class IdeologyLessonPlanInset : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属教案（CourseLessonPlan.Id）。</summary>
    public Guid LessonPlanId { get; set; }

    /// <summary>课程 Id（冗余，便于按课程统计）。</summary>
    public Guid CourseId { get; set; }

    /// <summary>引用的思政案例（自定义思政点时为 null）。</summary>
    public Guid? CaseId { get; set; }

    /// <summary>引用的思政元素（自定义思政点时可空）。</summary>
    public Guid? ElementId { get; set; }

    /// <summary>插入的教学环节。</summary>
    public IdeologyInsetPosition Position { get; set; } = IdeologyInsetPosition.Import;

    /// <summary>自定义思政点标题（引用案例时为快照，便于案例被删后仍可展示）。</summary>
    public string? Title { get; set; }

    /// <summary>自定义思政点内容 / 教师补充说明。</summary>
    public string? Content { get; set; }

    public int SortOrder { get; set; }

    protected IdeologyLessonPlanInset()
    {
    }

    public IdeologyLessonPlanInset(Guid id, Guid lessonPlanId, Guid courseId) : base(id)
    {
        LessonPlanId = lessonPlanId;
        CourseId = courseId;
    }
}
