namespace KnowledgeHub.Resources.Enums;

/// <summary>
/// 资源媒体处理状态（冗余到 Resource 上，列表页可快速渲染）。
/// </summary>
public enum ResourceMediaStatus : byte
{
    /// <summary>无需处理或尚未入队。</summary>
    None = 0,
    /// <summary>媒体处理中（缩略图/预览生成中）。</summary>
    Processing = 10,
    /// <summary>全部生成物就绪。</summary>
    Ready = 30,
    /// <summary>部分生成物失败（其余可用）。</summary>
    PartialFailed = 35,
    /// <summary>处理失败。</summary>
    Failed = 40
}
