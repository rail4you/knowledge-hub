namespace KnowledgeHub.Cooperation;

/// <summary>产教合作类型（需求类型）。</summary>
public enum CooperationDemandType : byte
{
    /// <summary>人才培养与订单班。</summary>
    TalentTraining = 0,

    /// <summary>实习实训与就业。</summary>
    Internship = 1,

    /// <summary>课程教材及资源共建。</summary>
    Curriculum = 2,

    /// <summary>基地和设备共享。</summary>
    BaseSharing = 3,

    /// <summary>教师企业实践与培训。</summary>
    TeacherPractice = 4,

    /// <summary>技术研发与成果转化。</summary>
    TechTransfer = 5,

    /// <summary>产业学院与联合体。</summary>
    IndustrialCollege = 6
}

/// <summary>合作需求状态。</summary>
public enum CooperationDemandStatus : byte
{
    /// <summary>草稿（企业可继续修改）。</summary>
    Draft = 0,

    /// <summary>待审核（提交后进入学校初审 → 联盟终审）。</summary>
    PendingReview = 1,

    /// <summary>已发布（进入需求大厅）。</summary>
    Published = 2,

    /// <summary>已驳回。</summary>
    Rejected = 3,

    /// <summary>已关闭（不再接受响应）。</summary>
    Closed = 4,

    /// <summary>已归档。</summary>
    Archived = 5
}

/// <summary>需求可见范围。</summary>
public enum CooperationDemandScope : byte
{
    /// <summary>仅入驻校可见。</summary>
    HostSchoolOnly = 0,

    /// <summary>联盟内学校可见。</summary>
    Alliance = 1,

    /// <summary>平台公开（所有学校可见）。</summary>
    Public = 2
}

/// <summary>学校响应状态。</summary>
public enum CooperationResponseStatus : byte
{
    /// <summary>已提交意向，等待企业确认。</summary>
    Submitted = 0,

    /// <summary>企业已接受（可据此建立项目台账）。</summary>
    Accepted = 1,

    /// <summary>企业已婉拒。</summary>
    Declined = 2,

    /// <summary>学校主动撤回。</summary>
    Withdrawn = 3
}

/// <summary>合作项目阶段。</summary>
public enum CooperationProjectStage : byte
{
    /// <summary>洽谈确认中。</summary>
    Confirming = 0,

    /// <summary>执行中。</summary>
    Executing = 1,

    /// <summary>待验收。</summary>
    AcceptancePending = 2,

    /// <summary>已归档。</summary>
    Archived = 3,

    /// <summary>已终止。</summary>
    Terminated = 4
}
