namespace KnowledgeHub.DoubleHigh.Enums;

/// <summary>
/// 指标的数据来源类型。Manual 表示由教师手工填报，其余为系统自动统计。
/// </summary>
public enum DoubleHighDataSourceType
{
    Manual = 0,
    ResourceCount = 1,
    CourseCount = 2,
    MicroMajorCount = 3,
    PracticumProjectCount = 4,
    NewsArticleCount = 5,
    MicroMajorEnrollmentCount = 6,
    PracticumEnrollmentCount = 7,

    /// <summary>课程选课人次（学生学习）。</summary>
    CourseEnrollmentCount = 8,

    /// <summary>课程学习完成率（%，学生学习）。</summary>
    CourseCompletionRate = 9,

    /// <summary>就业率（%，就业招聘）。</summary>
    EmploymentRate = 10
}
