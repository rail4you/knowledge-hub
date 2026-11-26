namespace KnowledgeHub.Learning.Enums;

/// <summary>
/// 学生行为埋点类型。
/// <para>
/// 用途：① 学习画像（偏好类型 / 活跃时段 / 连续性）；② 推荐的「行为依据」；
/// ③ 教师端学情时间线。只记事实，不做聚合（聚合由画像服务计算）。
/// </para>
/// </summary>
public enum StudentActivityAction
{
    /// <summary>学习章节 / 上报学习进度。</summary>
    StudyChapter = 0,

    /// <summary>作答习题。</summary>
    AnswerExercise = 1,

    /// <summary>提交考试。</summary>
    SubmitExam = 2,

    /// <summary>提交作业。</summary>
    SubmitAssignment = 3,

    /// <summary>观看课程视频。</summary>
    WatchVideo = 4,

    /// <summary>签到。</summary>
    CheckIn = 5,

    /// <summary>课程讨论区发帖 / 回复。</summary>
    PostDiscussion = 6,

    /// <summary>提交小组任务。</summary>
    SubmitGroupTask = 7,

    /// <summary>完成课堂练习。</summary>
    Practice = 8
}
