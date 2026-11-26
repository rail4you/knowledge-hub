using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程教学信息（与 <see cref="Course"/> 一对一，缺失时不建行）。
/// <para>
/// 来源：kg-edu 的 <c>course_infos</c>。它承载「对外展示 / 评估材料」需要但
/// 课程实体本身不该承担的长文本：教学目标、毕业要求、知识逻辑结构等。
/// 单列一张表而不是塞进 <see cref="Course"/>，是因为这些字段体积大且大多为空，
/// 课程列表查询不应该把它们一起拖出来。
/// </para>
/// </summary>
public class CourseInfo : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属课程（唯一）。</summary>
    public Guid CourseId { get; set; }

    /// <summary>课程背景（为什么开这门课）。</summary>
    public string? Background { get; set; }

    /// <summary>教学目标。</summary>
    public string? Objectives { get; set; }

    /// <summary>课程定位（面向什么专业 / 层次 / 学习者）。</summary>
    public string? Target { get; set; }

    /// <summary>课程亮点 / 特色。</summary>
    public string? Highlights { get; set; }

    /// <summary>课程介绍（长文本，与 Course.Description 的短简介区分）。</summary>
    public string? Introduction { get; set; }

    /// <summary>课程知识逻辑结构（章节之间的递进关系，学生端「课程结构」Tab）。</summary>
    public string? Structure { get; set; }

    /// <summary>毕业要求支撑（专业认证 / 双高评估会直接取这一段）。</summary>
    public string? GraduationRequirements { get; set; }

    private CourseInfo() { }

    public CourseInfo(Guid id, Guid courseId) : base(id)
    {
        CourseId = courseId;
    }
}
