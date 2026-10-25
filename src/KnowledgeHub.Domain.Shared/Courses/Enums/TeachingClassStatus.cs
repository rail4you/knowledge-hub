namespace KnowledgeHub.Courses.Enums;

/// <summary>
/// 教学班状态。
///
/// 说明：班级是「教学组织单元」，与「选课记录」是两件事 ——
/// 一个班的学生可以在不同的课程拥有不同的选课归属；
/// 班级归属变化不会静默迁移既有成绩。
/// </summary>
public enum TeachingClassStatus : byte
{
    /// <summary>草稿：尚未开放添加学生</summary>
    Draft = 0,

    /// <summary>进行中：可添加学生、可按班级批量选课</summary>
    Active = 1,

    /// <summary>已结课：不再接受新学生，历史数据保留</summary>
    Archived = 2,
}
