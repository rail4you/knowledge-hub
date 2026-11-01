namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 库内内容准入状态（V2.0）。
///
/// <para>
/// 与 <c>Resource.Status</c>（内容本身是否审核通过）是<b>两个正交维度</b>：
/// 内容可以先审核通过再入多个库，也可以「内容+准入」一次审完。
/// </para>
///
/// <para>状态流转：</para>
/// <code>
/// Draft ──提交──► PendingReview ──通过──► Published
///                    │                        │
///                    └──驳回──► Rejected       └──下架──► Withdrawn
/// </code>
/// </summary>
public enum ResourcePublicationStatus : byte
{
    /// <summary>草稿：已建发布记录但尚未提交审核（仅提交人可见）。</summary>
    Draft = 0,

    /// <summary>审核中：校审 / 联盟终审进行中，库内不展示。</summary>
    PendingReview = 1,

    /// <summary>已发布：库内可见，可按策略预览 / 下载 / 引用。</summary>
    Published = 2,

    /// <summary>已驳回：不展示，提交人可修改后重提。</summary>
    Rejected = 3,

    /// <summary>已下架：内容从该库撤出（历史引用按撤销处理）。</summary>
    Withdrawn = 4
}

/// <summary>
/// 贡献方类型 —— 企业案例入库时用于区分审核链路（合作校初审 + 联盟终审）。
/// </summary>
public enum PublicationSubmitterType : byte
{
    /// <summary>学校（教师 / 院校管理员）。</summary>
    School = 0,

    /// <summary>企业（企业人员提交的企业案例）。</summary>
    Enterprise = 1,

    /// <summary>平台运营（导入 / 运营补录）。</summary>
    Platform = 2
}
