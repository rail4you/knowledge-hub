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
    Archived = 2
}
