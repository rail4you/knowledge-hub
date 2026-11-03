namespace KnowledgeHub.DoubleHigh.Enums;

/// <summary>
/// 指标的评分规则（与权重一起在「项目配置」里设定，决定该指标如何折算得分）。
/// </summary>
public enum DoubleHighScoringRule
{
    /// <summary>比例制：完成度 = 实际值 / 目标值 × 100（上限 100）。</summary>
    Proportional = 0,

    /// <summary>达标制：实际值 ≥ 目标值得满分，否则得 0 分。</summary>
    Threshold = 1,

    /// <summary>直接计分：教师直接填写百分制得分（0~100）。</summary>
    Direct = 2,

    /// <summary>不计分：仅作为过程数据 / 说明性指标展示，不参与加权得分。</summary>
    None = 3
}
