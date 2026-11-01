namespace KnowledgeHub.Review;

/// <summary>
/// 审核目标类型 —— 决定「谁审、审几级、审完做什么」。
///
/// <para>
/// V2.0 把原先分散在资源（<c>ResourceAudit</c>）、联盟（<c>AllianceAudit</c>）里的审核，
/// 收敛到一个统一的审核单（<see cref="ReviewTicket"/>）上；本枚举是分流的第一维度。
/// </para>
/// </summary>
public enum ReviewTargetType : byte
{
    /// <summary>教学资源库本身（建库 / 改库 / 删库）。</summary>
    Library = 0,

    /// <summary>资源库内容准入与发布（资源 / 课程 / 实训进入某个库）。</summary>
    LibraryPublication = 1,

    /// <summary>共建任务交付物验收。</summary>
    BuildTaskDelivery = 2,

    /// <summary>企业主体认证（产教合作）。</summary>
    EnterpriseProfile = 3,

    /// <summary>招聘岗位发布审核。</summary>
    EmploymentJob = 4,

    /// <summary>产教合作需求公开审核。</summary>
    CooperationDemand = 5
}

/// <summary>
/// 审核动作类型 —— 同一目标可以做不同的事，每件事单独走审核。
/// </summary>
public enum ReviewActionType : byte
{
    /// <summary>新建（建库 / 建企业档案 / 建岗位）。</summary>
    Create = 0,

    /// <summary>修改（改名称 / 改可见性 / 改成员校）。</summary>
    Update = 1,

    /// <summary>删除（V2.0 一律归档，不硬删）。</summary>
    Delete = 2,

    /// <summary>准入（内容进入某个资源库）。</summary>
    Admit = 3,

    /// <summary>发布（岗位 / 需求公开）。</summary>
    Publish = 4,

    /// <summary>认证（企业主体资质）。</summary>
    Certify = 5,

    /// <summary>撤销（下架 / 撤权）。</summary>
    Revoke = 6
}

/// <summary>
/// 审核阶段。
///
/// <para>
/// 只有两个角色、两个阶段，与需求方确认的口径一致：
/// </para>
/// <list type="bullet">
/// <item><description><see cref="School"/>：院校管理员（<c>SchoolAdmin</c>）审核本校对象；
/// 校内私有 / 公开库到此为止（单级）。</description></item>
/// <item><description><see cref="League"/>：联盟管理员（<c>LeagueAdmin</c>，Host 侧）终审跨校对象；
/// 仅院校共建库 / 企业贡献等跨校场景需要。</description></item>
/// </list>
/// </summary>
public enum ReviewStage : byte
{
    /// <summary>院校初审（本校）。</summary>
    School = 0,

    /// <summary>联盟终审（跨校）。</summary>
    League = 1
}

/// <summary>审核单状态。</summary>
public enum ReviewTicketStatus : byte
{
    /// <summary>待审（当前阶段等待审核人处理）。</summary>
    Pending = 0,

    /// <summary>通过（全部阶段完成，可应用变更）。</summary>
    Approved = 1,

    /// <summary>驳回（提交人修改后可重新提交，轮次 +1）。</summary>
    Rejected = 2,

    /// <summary>已撤回（提交人主动撤回，未产生审核结论）。</summary>
    Cancelled = 3
}

/// <summary>单次审核动作的结论。</summary>
public enum ReviewDecision : byte
{
    /// <summary>通过当前阶段。</summary>
    Approved = 0,

    /// <summary>驳回（提交人需修改后重提）。</summary>
    Rejected = 1,

    /// <summary>退回补充材料（与驳回等价的状态效果，但语义更轻）。</summary>
    Returned = 2
}

/// <summary>
/// 审核明细上的动作（<see cref="ReviewAction"/> 用），描述「这次发生了什么操作」，
/// 与 <see cref="ReviewActionType"/>（描述「审核目标被做了什么」）是两个不同维度，不可混用。
/// </summary>
public enum ReviewOperation : byte
{
    /// <summary>提交（新提交或驳回后重提）。</summary>
    Submit = 0,

    /// <summary>通过当前阶段。</summary>
    Approve = 1,

    /// <summary>驳回。</summary>
    Reject = 2,

    /// <summary>退回补充材料。</summary>
    Return = 3,

    /// <summary>提交人撤回。</summary>
    Withdraw = 4
}
