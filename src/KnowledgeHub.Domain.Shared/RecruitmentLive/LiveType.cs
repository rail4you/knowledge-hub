namespace KnowledgeHub.RecruitmentLive;

/// <summary>
/// 直播类型：招聘直播（关联面试/学生）/ 课程直播（关联租户已有课程）。
/// 两者共用同一套直播间能力（房间码、参与者、聊天、WebRTC 信令），
/// 仅创建时的关联对象不同。
/// </summary>
public enum LiveType
{
    /// <summary>招聘直播：面向候选学生，默认类型</summary>
    Recruitment = 0,

    /// <summary>课程直播：必须关联租户内已存在的课程</summary>
    Course = 1
}
