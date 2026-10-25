using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 教学班成员（班级学员）。
///
/// 承载「某个学生属于某个班」这一关系，是「按班级筛选学生 / 按班级批量选课」
/// 的数据基础。
///
/// 注意：成员关系 ≠ 选课记录。学生被加入班级并不代表他已选修某门课程；
/// 选课由 StudentCourse 承载。
/// </summary>
public class TeachingClassMember : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>所属学校（= 班级所在租户）</summary>
    public Guid? TenantId { get; set; }

    /// <summary>班级 Id</summary>
    public Guid ClassId { get; set; }

    /// <summary>学生用户 Id</summary>
    public Guid StudentId { get; set; }

    /// <summary>学生姓名（冗余，便于列表直接展示，避免每次联表）</summary>
    public string? StudentName { get; set; }

    /// <summary>学号（冗余，取自用户账号，便于按学号查找）</summary>
    public string? StudentNumber { get; set; }

    /// <summary>加入时间</summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>成员来源</summary>
    public TeachingClassMemberSource Source { get; set; } = TeachingClassMemberSource.Manual;

    public TeachingClassMember()
    {
    }

    public TeachingClassMember(Guid id, Guid classId, Guid studentId) : base(id)
    {
        ClassId = classId;
        StudentId = studentId;
        JoinedAt = DateTime.UtcNow;
    }
}

/// <summary>班级成员来源</summary>
public enum TeachingClassMemberSource : byte
{
    /// <summary>手工添加</summary>
    Manual = 0,

    /// <summary>名单导入（Excel 等批量导入）</summary>
    Import = 1,

    /// <summary>学生自主加入</summary>
    SelfJoin = 2,
}
