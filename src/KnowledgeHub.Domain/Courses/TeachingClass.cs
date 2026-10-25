using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 教学班。
///
/// 解决的问题：平台此前所有「班级」都只是自由文本
/// （PracticumProject.ClassName / StudentResume.ClassName），
/// 无法按班级查询学生、无法按班级批量选课，也无法回答
/// 「这个学生的数据归哪个老师/哪个班」。
///
/// 语义边界（重要）：
///   · 教学班是「教学组织单元」，不是用户类型。
///   · 学生与班级的关系是「成员关系」，不是「选课记录」。
///   · 选课记录（StudentCourse）另行存在，ClassId 只是可选的来源标记。
///   · 班级归属变化不得静默迁移既有成绩 —— 退班只解除成员关系，
///     已产生的学习与考核记录仍归原课程与原教师。
/// </summary>
public class TeachingClass : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>所属学校（= ABP 租户）。租户过滤由 IMultiTenant 自动生效。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>班级名称，如「2026 级机电一班」</summary>
    public string Name { get; set; }

    /// <summary>所属专业</summary>
    public Guid? MajorId { get; set; }

    /// <summary>关联课程（可空：一个班可以服务于多门课）</summary>
    public Guid? CourseId { get; set; }

    /// <summary>归属教师：该班学生的教学数据责任教师</summary>
    public Guid? TeacherId { get; set; }

    /// <summary>年级（入学年份，如 2026）</summary>
    public int? GradeYear { get; set; }

    /// <summary>备注</summary>
    public string? Description { get; set; }

    /// <summary>班级状态</summary>
    public TeachingClassStatus Status { get; set; } = TeachingClassStatus.Active;

    /// <summary>班级成员</summary>
    public List<TeachingClassMember> Members { get; set; }

    public TeachingClass()
    {
        Members = new List<TeachingClassMember>();
    }

    public TeachingClass(Guid id, string name) : base(id)
    {
        Name = name;
        Members = new List<TeachingClassMember>();
    }

    public void AddMember(TeachingClassMember member)
    {
        Members.Add(member);
    }

    public void RemoveMember(Guid studentId)
    {
        Members.RemoveAll(m => m.StudentId == studentId);
    }
}
