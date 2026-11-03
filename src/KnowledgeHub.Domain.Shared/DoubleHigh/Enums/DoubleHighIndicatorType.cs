namespace KnowledgeHub.DoubleHigh.Enums;

/// <summary>
/// 指标的填报类型（决定教师在「填报指标」页面对该指标做什么）。
/// </summary>
public enum DoubleHighIndicatorType
{
    /// <summary>系统取数：由平台统计自动带出，教师不可修改。</summary>
    Auto = 0,

    /// <summary>填写数值：教师按实际情况填写数值。</summary>
    Number = 1,

    /// <summary>填写说明：教师填写文字说明。</summary>
    Text = 2,

    /// <summary>仅佐证：只要求上传佐证材料，不需要填报数值。</summary>
    EvidenceOnly = 3
}
