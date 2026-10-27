namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库的<b>跨校共享范围</b> —— 决定库内课程 / 资源能否被共享到其它学校。
///
/// <para>
/// 需求（PRD 4.1 / 4.2 / 4.3）：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>校级私有库</b>：不产生任何跨校共享，库内课程 / 资源仅供本校使用；
/// </description></item>
/// <item><description>
/// <b>校级共享库</b>：库是「校内共享 + 对外开放自学（其他学员）」，同样<b>不</b>跨校共享；
/// </description></item>
/// <item><description>
/// <b>校际共建库</b>：允许跨校共享，但只能共享给<b>参建单位</b>（共建成员学校），不能共享给任意学校。
/// </description></item>
/// </list>
///
/// <para>
/// 未挂载到任何教学资源库的课程 / 资源不受限制（保持平台原有「可共享给任意学校」的行为）。
/// </para>
/// </summary>
public enum ResourceLibrarySharingScope : byte
{
    /// <summary>无限制：内容未挂载到教学资源库。</summary>
    Unrestricted = 0,

    /// <summary>仅限校内：校级私有库 / 校级共享库。</summary>
    SchoolOnly = 1,

    /// <summary>仅限共建成员学校：校际共建库。</summary>
    CoBuildMembersOnly = 2
}
