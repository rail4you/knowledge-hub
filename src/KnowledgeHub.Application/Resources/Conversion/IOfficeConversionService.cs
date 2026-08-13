using System.Threading;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// Office 文档预览转换服务：将 PPTX/DOCX/XLSX 转为 PDF，供前端 pdfjs 渲染。
/// 实现通常为 Gotenberg（内部基于 LibreOffice headless）。
/// 转换任务由 Hangfire 队列调度，实际执行通过 ConversionConcurrencyManager 限流。
/// </summary>
public interface IOfficeConversionService
{
    /// <summary>
    /// 将指定路径的 Office 文件转为 PDF，返回 PDF 的绝对路径。
    /// 实现内部应做：缓存检查 → 并发闸门 → 转换 → 缓存写入。
    /// 同一 resourceId 的并发请求会复用同一次转换结果。
    /// </summary>
    /// <param name="resourceId">资源 ID（缓存 key）</param>
    /// <param name="sourcePath">源文件绝对路径（PPTX/DOCX/XLSX）</param>
    /// <param name="serviceName">并发限流分组（默认 "preview"；后台重处理用 "reprocess"）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PDF 文件的绝对路径</returns>
    Task<string> ConvertToPdfAsync(
        string resourceId,
        string sourcePath,
        string serviceName = "preview",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否已存在缓存的 PDF。
    /// </summary>
    bool HasCachedPdf(string resourceId);

    /// <summary>
    /// 完整 PDF 缓存是否对应当前源文件（存在且 meta 有效）。
    /// 用于 /preview-pdf-info 判断转换是否已就绪。
    /// </summary>
    bool HasValidCachedPdf(string resourceId, string sourcePath);

    /// <summary>
    /// 该资源是否已有转换任务在排队/执行中（Hangfire job 已 enqueue 或正在转换）。
    /// 用于避免同一资源被重复 enqueue。
    /// </summary>
    bool IsInFlight(string resourceId);

    /// <summary>
    /// 清除缓存的 PDF（源文件更新后调用）。
    /// </summary>
    void InvalidateCache(string resourceId);

    /// <summary>
    /// 获取单页 PDF 的缓存路径（兼容旧端点，已不主动生成）。
    /// </summary>
    string GetPagePdfPath(string resourceId, int pageNumber);

    /// <summary>
    /// 截断/损坏 PPTX 的 ZIP 修复缓存路径（{CacheDirectory}/{resourceId}.repaired.pptx）。
    /// 可能不存在；用于 PPTX 幻灯片提取时作为缺失/损坏条目的回退源。
    /// </summary>
    string GetRepairedPptxPath(string resourceId);
}

/// <summary>
/// Office 转换异常。前端捕获后降级为下载原文件。
/// </summary>
public class OfficeConversionException : System.Exception
{
    public OfficeConversionException(string message) : base(message) { }
    public OfficeConversionException(string message, System.Exception inner) : base(message, inner) { }
}
