namespace KnowledgeHub.Courses.Enums;

/// <summary>
/// 课程知识点节点类型。
/// 知识点树只表达「教学结构」：单元（可再包含子单元）与知识项（叶子，挂习题/视频/资源）。
/// </summary>
public enum KnowledgeNodeType
{
    /// <summary>知识单元：目录性节点，可包含子单元或知识项。</summary>
    Unit = 0,

    /// <summary>知识项：叶子节点，是掌握度与练习的最小粒度。</summary>
    Item = 1,

    /// <summary>知识元：知识项下的细分点（可选，用于精确到「定义/性质/例题」这类颗粒）。</summary>
    Element = 2
}
