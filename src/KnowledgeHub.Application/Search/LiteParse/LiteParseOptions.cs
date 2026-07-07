namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// Liteparse HTTP 服务连接与行为配置。
/// 对应配置文件节点: Liteparse
/// </summary>
public class LiteParseOptions
{
    /// <summary>
    /// Liteparse 服务 BaseAddress（开发: http://localhost:15000, 生产 docker: http://liteparse:5707）。
    /// </summary>
    public string Host { get; set; } = "http://localhost:15000";

    /// <summary>
    /// 单次 /parse 请求超时（秒）。大 PDF + OCR 可能耗时较长，默认 300s。
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 透传给 liteparse 的 DPI 配置（影响 OCR 精度）。DOCX/PPTX/XLSX 不受影响。
    /// </summary>
    public int Dpi { get; set; } = 300;
}