namespace KnowledgeHub.Resources.Enums;

/// <summary>
/// 资源所属模块（栏目）。
///
/// <para>对应采购要求「后台选择资源所属模块」：资源中心的一份资源归属一个业务栏目，
/// 前台可据此按模块筛选。与「资源库」正交 —— 资源库是归属单位（校内/共建），
/// 模块是业务栏目（政策经验 / 课标教材 …）。</para>
/// </summary>
public enum ResourceModule
{
    /// <summary>未指定（历史数据默认值，前台归入「未分类」）。</summary>
    None = 0,

    /// <summary>课程资源（常规教学资源）。</summary>
    Course = 1,

    /// <summary>职业教育。</summary>
    Vocational = 2,

    /// <summary>融合教育。</summary>
    Inclusive = 3,

    /// <summary>政策经验。</summary>
    Policy = 4,

    /// <summary>大讲堂。</summary>
    LectureHall = 5,

    /// <summary>课标教材。</summary>
    Textbook = 6,

    /// <summary>教具辅具。</summary>
    TeachingAid = 7,

    /// <summary>家长课堂。</summary>
    ParentClass = 8,

    /// <summary>学生成长。</summary>
    StudentGrowth = 9,

    /// <summary>专题教育。</summary>
    Thematic = 10,
}
