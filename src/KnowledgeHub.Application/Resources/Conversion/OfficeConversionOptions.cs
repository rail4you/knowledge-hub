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
    /// 每类服务默认并发转换数（ConversionConcurrencyManager 的初始值）。
    /// 可在运行期通过 API 调整（默认 1 = 严格串行，防止 LibreOffice 打爆 CPU）。
    /// </summary>
    public int MaxConcurrentConversions { get; set; } = 1;

    /// <summary>
    /// 在线 PDF 预览的最大源文件大小（字节）。超过该大小的 Office 文档不做转换，
    /// 直接返回"文件过大请下载"（超大文件转 PDF 会长期占满服务器 CPU/内存）。
    /// 默认 100MB。
    /// </summary>
    public long MaxPreviewFileSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// 大文件图片降采样阈值（字节）。源文件超过该大小时，转换时启用
    /// Gotenberg 的 reduceImageResolution + maxImageResolution + quality，
    /// 把嵌入图片降到 <see cref="MaxImageResolutionDpi"/> DPI，显著减小 PDF 体积
    /// 与 LibreOffice 处理内存/CPU。默认 30MB。
    /// </summary>
    public long ReduceImageResolutionThresholdBytes { get; set; } = 30 * 1024 * 1024;

    /// <summary>
    /// 大文件降采样目标 DPI（75/150/300/600/1200，Gotenberg 枚举）。
    /// 0 = 不启用降采样。默认 150（在线预览清晰度足够且显著减负）。
    /// </summary>
    public int MaxImageResolutionDpi { get; set; } = 150;

    /// <summary>
    /// 大文件降采样时 JPEG 导出质量（1-100）。0 = 保持默认（90）。
    /// 默认 75。
    /// </summary>
    public int JpegQuality { get; set; } = 75;

    /// <summary>
    /// 缓存目录（相对于 IFileStorageService.RootPath）。
    /// 缓存文件: {RootPath}/{CacheDirectory}/{resourceId}.pdf
    /// </summary>
    public string CacheDirectory { get; set; } = "converted";

    /// <summary>
    /// 后台重处理（预热老资源 PDF 缓存）的轮询周期（分钟）。
    /// 0 或负数 = 禁用后台重处理。
    /// </summary>
    public int ReprocessPeriodMinutes { get; set; } = 5;

    /// <summary>
    /// 后台重处理每轮最多处理的资源数。默认 5 个，逐条串行转换，
    /// 避免长期霸占全局转换队列阻塞用户预览。
    /// </summary>
    public int ReprocessBatchSize { get; set; } = 5;
}
