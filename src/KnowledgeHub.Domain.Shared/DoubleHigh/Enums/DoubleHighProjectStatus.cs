namespace KnowledgeHub.DoubleHigh.Enums;

/// <summary>
/// 评估项目流程状态。
/// </summary>
public enum DoubleHighProjectStatus
{
    /// <summary>配置中：指标尚未定稿，教师还能调整权重与评分规则。</summary>
    Draft = 0,

    /// <summary>填报中：已开始填报，指标配置锁定。</summary>
    Active = 1,

    /// <summary>已提交：教师完成填报并提交，内容锁定。</summary>
    Submitted = 3,

    /// <summary>已关闭：项目归档。</summary>
    Closed = 2
}
