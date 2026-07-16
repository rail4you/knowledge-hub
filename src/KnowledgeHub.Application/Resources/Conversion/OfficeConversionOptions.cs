namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// Office 文档 → PDF 转换配置（LibreOffice headless 模式）。
/// 对应配置文件节点: OfficeConversion
/// </summary>
public class OfficeConversionOptions
{
    /// <summary>
    /// soffice 可执行文件路径。
    /// 开发（macOS brew）: /opt/homebrew/bin/soffice
    /// 开发（Linux apt）: /usr/bin/soffice
    /// Docker（基于 debian-slim）: /usr/bin/soffice
    /// 留空则自动从 PATH 查找 "soffice"。
    /// </summary>
    public string SofficePath { get; set; } = "soffice";

    /// <summary>
    /// 转换超时时间（秒）。默认 120s，80MB PPTX 实测约 9s。
    /// 超时后会抛出 OfficeConversionException，前端降级为下载。
    /// </summary>
    public int ConversionTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 最大并发转换数。LibreOffice 启动开销大，并发过多会爆 CPU。
    /// 超出时排队等待。
    /// </summary>
    public int MaxConcurrentConversions { get; set; } = 2;

    /// <summary>
    /// 缓存目录（相对于 IFileStorageService.RootPath）。
    /// 缓存文件: {RootPath}/{CacheDirectory}/{resourceId}.pdf
    /// </summary>
    public string CacheDirectory { get; set; } = "converted";
}