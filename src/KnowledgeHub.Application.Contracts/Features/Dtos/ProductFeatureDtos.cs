namespace KnowledgeHub.Features.Dtos;

/// <summary>
/// 产品能力开关（前端菜单 / 页面按此决定是否展示增值模块）。
///
/// <para>取代了早期「版本 + 许可证」的 <c>EditionDto</c>：只暴露功能开关，不涉及任何授权信息。</para>
/// </summary>
public class ProductFeatureDto
{
    /// <summary>允许创建的租户数量上限（-1 表示不限）。</summary>
    public int MaxTenantCount { get; set; }

    /// <summary>是否启用多校联盟协同。</summary>
    public bool IsAllianceEnabled { get; set; }

    /// <summary>是否启用两级审核（院校审核 → 联盟审核）。</summary>
    public bool IsTwoLevelApprovalEnabled { get; set; }

    /// <summary>是否启用特殊教育扩展模块。</summary>
    public bool IsSpecialEducationEnabled { get; set; }
}
