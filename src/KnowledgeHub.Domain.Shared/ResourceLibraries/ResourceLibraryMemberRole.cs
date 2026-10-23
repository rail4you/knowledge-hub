namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库成员角色（多校共建）。
/// </summary>
public enum ResourceLibraryMemberRole : byte
{
    /// <summary>主建学校：库的所有者，拥有全部管理权（编辑库、增删成员、发布/归档）。</summary>
    Owner = 0,

    /// <summary>合作学校：参与共建，可浏览库内资源/课程，不可变更库本身。</summary>
    Partner = 1
}
