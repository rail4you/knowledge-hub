namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// Office 文档 → PDF 转换配置（Gotenberg HTTP 服务）。
/// 对应配置文件节点: OfficeConversion
/// </summary>
public class OfficeConversionOptions
{
    /// <summary>
    /// Gotenberg 服务地址。
    /// 开发（Docker）: http://localhost:3000
    /// 生产（Docker 内网）: http://gotenberg:3000
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>
    /// 转换超时时间（秒）。默认 120s，80MB PPTX 实测约 9s。
    /// 超时后会抛出 OfficeConversionException，前端降级为下载。
    /// </summary>
    public int ConversionTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 最大并发转换数。LibreOffice 转换是单进程串行，并发过多会打爆 CPU。
    /// 超出时排队等待。
    /// </summary>
    public int MaxConcurrentConversions { get; set; } = 2;

    /// <summary>
    /// 缓存目录（相对于 IFileStorageService.RootPath）。
    /// 缓存文件: {RootPath}/{CacheDirectory}/{resourceId}.pdf
    /// </summary>
    public string CacheDirectory { get; set; } = "converted";
}
