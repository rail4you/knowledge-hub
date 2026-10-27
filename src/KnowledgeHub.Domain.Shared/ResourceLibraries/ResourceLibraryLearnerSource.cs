namespace KnowledgeHub.ResourceLibraries;

/// <summary>教学资源库学员来源（PRD 5.1 数据归属矩阵）。</summary>
public enum ResourceLibraryLearnerSource : byte
{
    /// <summary>自学外部用户（未通过班级导入，自行注册学习）→ 归入主建老师「其他学员」目录。</summary>
    SelfLearner = 0,

    /// <summary>老师自建班级导入的学员 → 归入班级目录。</summary>
    ClassStudent = 1
}
