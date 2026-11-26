namespace KnowledgeHub.Classroom.Enums;

/// <summary>签到场次状态。</summary>
public enum CheckInSessionStatus
{
    /// <summary>进行中：学生可签到。</summary>
    Active = 0,

    /// <summary>已结束：不再接受签到。</summary>
    Closed = 1
}

/// <summary>课堂练习场次状态。</summary>
public enum ClassroomPracticeStatus
{
    /// <summary>进行中：学生可取题作答。</summary>
    Active = 0,

    /// <summary>已结束：不再接受提交（已提交的仍可看结果）。</summary>
    Closed = 1
}

/// <summary>小组任务状态。</summary>
public enum GroupTaskStatus
{
    /// <summary>草稿：教师还在分组，学生不可见。</summary>
    Draft = 0,

    /// <summary>已发布：学生可通过 token / 学生端看到并提交。</summary>
    Published = 1,

    /// <summary>已结束：不接受新的提交。</summary>
    Closed = 2
}

/// <summary>分组方式。</summary>
public enum GroupMode
{
    /// <summary>教师手工指定成员分组。</summary>
    TeacherAssigned = 0,

    /// <summary>按选课名单随机平均分组。</summary>
    RandomAuto = 1
}

/// <summary>
/// 免登录链接（扫码/短链）指向的场次类型。
/// 只用于 token 解析，不落库。
/// </summary>
public enum ClassroomTokenKind
{
    CheckIn = 0,
    GroupTask = 1,
    Practice = 2
}
