namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库状态。
///
/// <para>
/// 资源库是「主建学校」拥有的租户级实体，合作学校通过 <c>ResourceLibraryMember</c> 加入。
/// 状态决定可见范围：
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Draft"/>：草稿，仅主建学校（库所有者）内部可见。</description></item>
/// <item><description><see cref="Active"/>：已发布，主建学校与合作学校均可访问。</description></item>
/// <item><description><see cref="Archived"/>：已归档，只读保留，不再参与共建。</description></item>
/// </list>
/// </summary>
public enum ResourceLibraryStatus : byte
{
    /// <summary>草稿：仅主建学校可见。</summary>
    Draft = 0,

    /// <summary>已发布：主建学校与合作学校共享可见。</summary>
    Active = 1,

    /// <summary>已归档：只读保留。</summary>
    Archived = 2,

    // ── V2.0 审核态（只追加，不改历史值） ──

    /// <summary>待院校审核：建库已提交，本校院校管理员审。审核中不对成员校 / 学生可见。</summary>
    PendingReview = 3,

    /// <summary>待联盟终审：院校初审已过（仅院校共建库），联盟管理员审。</summary>
    PendingLeagueReview = 4,

    /// <summary>已驳回：提交人修改后可重新提交（轮次 +1）。</summary>
    Rejected = 5,

    /// <summary>变更审核中：库仍按旧配置对外服务，变更存在审核单快照里。</summary>
    ChangePending = 6,

    /// <summary>删除审核中：库仍正常服务，通过后转入 <see cref="Archived"/>（不硬删）。</summary>
    DeletePending = 7
}
