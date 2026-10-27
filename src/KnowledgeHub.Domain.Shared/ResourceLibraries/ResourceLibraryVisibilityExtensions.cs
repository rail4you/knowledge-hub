namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// <see cref="ResourceLibraryVisibility"/> 的 PRD 语义扩展。
///
/// <para>
/// PRD 第 2.1 节把资源库分为三种形态，与平台枚举的对应关系是：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ResourceLibraryVisibility.Private"/> → <b>校级私有库</b>（非共享资源库）：
/// 前台所有人可见可浏览，仅建设老师导入班级的学员可学习；不允许参建单位；不对外开放自学。
/// </description></item>
/// <item><description>
/// <see cref="ResourceLibraryVisibility.Public"/> → <b>校级共享库</b>（单校共享）：
/// 对外开放学习（其他学员需手机号 + 实名认证）；不允许参建单位。
/// </description></item>
/// <item><description>
/// <see cref="ResourceLibraryVisibility.CoBuilt"/> → <b>校际共建库</b>：
/// 主建单位 + 参建单位；允许对外开放自学（规则同校级共享库）。
/// </description></item>
/// </list>
/// </summary>
public static class ResourceLibraryVisibilityExtensions
{
    /// <summary>PRD 形态名。</summary>
    public static string GetFormName(this ResourceLibraryVisibility visibility)
    {
        return visibility switch
        {
            ResourceLibraryVisibility.Private => "校级私有库",
            ResourceLibraryVisibility.Public => "校级共享库",
            ResourceLibraryVisibility.CoBuilt => "校际共建库",
            _ => "未知形态"
        };
    }

    /// <summary>是否允许参建单位（仅校际共建库）。</summary>
    public static bool AllowsPartnerSchool(this ResourceLibraryVisibility visibility)
        => visibility == ResourceLibraryVisibility.CoBuilt;

    /// <summary>
    /// 默认是否「对外开放自学」（其他学员经手机号 + 实名认证后可学习）。
    /// 校级私有库不对外开放；校级共享库 / 校际共建库对外开放。
    /// </summary>
    public static bool DefaultOpenToExternalLearners(this ResourceLibraryVisibility visibility)
        => visibility != ResourceLibraryVisibility.Private;

    /// <summary>该形态下的跨校共享范围。</summary>
    public static ResourceLibrarySharingScope GetSharingScope(this ResourceLibraryVisibility visibility)
    {
        return visibility switch
        {
            ResourceLibraryVisibility.Private => ResourceLibrarySharingScope.SchoolOnly,
            ResourceLibraryVisibility.Public => ResourceLibrarySharingScope.SchoolOnly,
            ResourceLibraryVisibility.CoBuilt => ResourceLibrarySharingScope.CoBuildMembersOnly,
            _ => ResourceLibrarySharingScope.Unrestricted
        };
    }
}
