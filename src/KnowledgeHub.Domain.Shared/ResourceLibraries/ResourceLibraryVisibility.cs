namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库可见性。
///
/// <para>
/// 需求「租户（学校）下可以有多个资源库，资源库可以是私有、公开、共建」：
/// 可见性决定<b>谁能看到这个库、以及谁能往库里共享内容</b>。
/// </para>
///
/// <list type="bullet">
/// <item><description>
/// <see cref="Private"/> 私有库：仅<b>被院校管理员分配到该库的教师</b>可见；
/// 库归属学校（主学校）的院校管理员始终可见并可管理。
/// </description></item>
/// <item><description>
/// <see cref="Public"/> 公开库：<b>归属学校（主学校）的全体教师</b>可见，
/// 且都可以向库内共享内容；不含关联学校。
/// </description></item>
/// <item><description>
/// <see cref="CoBuilt"/> 院校共建库：由<b>主学校</b>主建，<b>关联学校</b>参与共建；
/// 主学校与全部关联学校的教师共享该库，可互相看到对方共享进来的内容。
/// </description></item>
/// </list>
///
/// <para>
/// 注意：可见性只描述「库内成员的横向共享范围」，
/// 与门户公开展示开关 <c>ResourceLibrary.IsPublic</c> 是两个正交维度。
/// </para>
/// </summary>
public enum ResourceLibraryVisibility : byte
{
    /// <summary>私有库：仅被分配教师 + 本校院校管理员可见。</summary>
    Private = 0,

    /// <summary>公开库：本校全体教师可见，可向库内共享内容。</summary>
    Public = 1,

    /// <summary>院校共建库：主学校 + 关联学校共享。</summary>
    CoBuilt = 2
}
