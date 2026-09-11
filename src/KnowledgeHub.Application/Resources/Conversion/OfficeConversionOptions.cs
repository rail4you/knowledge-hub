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
    /// PPTX 媒体预压缩开关。源文件超过 <see cref="PptxPreprocessThresholdBytes"/> 且
    /// 媒体（GIF/PNG/JPEG）超过 <see cref="PptxMediaCompressThresholdBytes"/> 时，
    /// 用 ffmpeg 先把大图压小再交给 LibreOffice，显著降低转换峰值内存与耗时。
    /// 预压缩产物缓存到 converted/{resourceId}.light.pptx。
    /// </summary>
    public bool EnablePptxPreprocess { get; set; } = true;

    /// <summary>
    /// 触发 PPTX 媒体预压缩的源文件大小阈值（字节）。默认 30MB。
    /// 超大 PPTX 必然是大图/动画 GIF 撑起来的，预处理收益最大。
    /// </summary>
    public long PptxPreprocessThresholdBytes { get; set; } = 30 * 1024 * 1024;

    /// <summary>
    /// 单张媒体（GIF/PNG/JPEG）超过该大小（字节）才压缩。默认 500KB。
    /// </summary>
    public long PptxMediaCompressThresholdBytes { get; set; } = 512 * 1024;

    /// <summary>
    /// 预压缩后图片最长边像素。预览是静态 PDF，清晰度要求不高，默认 900px。
    /// 0 = 不缩放（仅转格式/抽帧）。
    /// </summary>
    public int PptxMaxImageDimension { get; set; } = 900;

    /// <summary>
    /// 预压缩时 JPEG 导出质量（1-31，ffmpeg -q:v，越小越清晰）。默认 4。
    /// </summary>
    public int PptxJpegQuality { get; set; } = 4;

    /// <summary>
    /// ffmpeg 可执行文件路径。开发机与 API 容器镜像均内置 ffmpeg，默认直接用命令名。
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>
    /// 单个媒体 ffmpeg 处理超时（秒）。默认 60s。
    /// 注意：视频索引压缩等长任务会显式传入更长的超时。
    /// </summary>
    public int FfmpegTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 全局 ffmpeg/pdftoppm 并发进程上限（跨缩略图 / PPTX 预压缩 / 视频索引 / 视频分析共享）。
    /// 默认 2（与 2 核机器匹配），防止并发进程打爆 CPU。
    /// </summary>
    public int FfmpegMaxConcurrency { get; set; } = 2;

    /// <summary>
    /// 视频编码线程数上限（ffmpeg -threads）。默认 2，避免抢占 API/其他任务 CPU。
    /// </summary>
    public int FfmpegThreads { get; set; } = 2;

    /// <summary>
    /// PDF 首页光栅化可执行文件路径（poppler 的 pdftoppm）。
    /// 开发机（brew poppler）与 API 容器（apt poppler-utils）均可用；缺失时缩略图回退为图标。
    /// </summary>
    public string PdftoppmPath { get; set; } = "pdftoppm";

    /// <summary>PDF 首页光栅化超时（秒）。默认 60s。</summary>
    public int PdftoppmTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 预压缩时是否剥离嵌入字体（PPTX 嵌入的 OTF/TTF 可达数 MB~数十 MB，
    /// 剥离后由 LibreOffice 用 Noto CJK 兜底渲染，预览精度足够）。
    /// 默认 true（收益大，本地实测文本渲染正常）。
    /// </summary>
    public bool StripEmbeddedFonts { get; set; } = true;

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
