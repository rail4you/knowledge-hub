namespace KnowledgeHub.Learning.Enums;

/// <summary>
/// 推荐类型（对齐 kg-edu 的 learning_recommendation.recommendation_type）。
/// 四类来源：薄弱知识点 / 前置知识 / 未完成章节 / 学习行为。
/// </summary>
public enum StudentRecommendationType
{
    /// <summary>薄弱知识点：掌握度低于判定线，建议练题或回看。</summary>
    WeakKnowledge = 0,

    /// <summary>前置知识：后续知识点要用的前置还没掌握。</summary>
    Prerequisite = 1,

    /// <summary>未完成章节：进度未满且近期无活动。</summary>
    IncompleteChapter = 2,

    /// <summary>学习行为：活跃度下降 / 学习断档，建议保持节奏。</summary>
    LearningHabit = 3
}

/// <summary>推荐生命周期（kg-edu 的 pending / viewed / in_progress / completed / dismissed）。</summary>
public enum StudentRecommendationStatus
{
    Pending = 0,
    Viewed = 1,
    InProgress = 2,
    Completed = 3,
    Dismissed = 4
}
