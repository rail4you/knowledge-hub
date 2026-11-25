namespace KnowledgeHub.CourseIdeology.Enums;

/// <summary>
/// 思政案例状态。
///
/// <para>本模块**不做审核流**（老师自建自用）：草稿 → 发布即可；发布后可用于图谱 / 覆盖分析 / 教案推荐。
/// 保留草稿态是为了「AI 生成 → 老师修订 → 发布」这条链路。</para>
/// </summary>
public enum IdeologyCaseStatus : byte
{
    /// <summary>草稿（AI 生成初稿默认落这里）。</summary>
    Draft = 0,

    /// <summary>已发布（正常可用）。</summary>
    Published = 10,

    /// <summary>已下架（保留数据、不再参与统计与推荐）。</summary>
    Archived = 20
}

/// <summary>案例素材来源类别。</summary>
public enum IdeologySourceType : byte
{
    /// <summary>教材。</summary>
    Textbook = 0,

    /// <summary>权威媒体（新华网 / 人民网 / 央视等）。</summary>
    AuthorityMedia = 1,

    /// <summary>法律法规原文。</summary>
    Law = 2,

    /// <summary>校企合作单位提供的素材。</summary>
    Enterprise = 3,

    /// <summary>AI 生成（必须人工核验）。</summary>
    AiGenerated = 4,

    /// <summary>其它（须在出处里写明）。</summary>
    Other = 5
}

/// <summary>案例在课堂上的融入方式。</summary>
public enum IdeologyUsageMethod : byte
{
    /// <summary>课程导入 · 讨论。</summary>
    Import = 0,

    /// <summary>案例分析。</summary>
    CaseAnalysis = 1,

    /// <summary>课堂讨论。</summary>
    Discussion = 2,

    /// <summary>实训点评。</summary>
    PracticeComment = 3,

    /// <summary>拓展阅读。</summary>
    ExtendedReading = 4
}

/// <summary>教案里的思政融入环节位置。</summary>
public enum IdeologyInsetPosition : byte
{
    /// <summary>导入。</summary>
    Import = 0,

    /// <summary>讲授。</summary>
    Lecture = 1,

    /// <summary>实训 / 练习。</summary>
    Practice = 2,

    /// <summary>小结。</summary>
    Summary = 3,

    /// <summary>课后作业 / 拓展。</summary>
    Homework = 4
}
