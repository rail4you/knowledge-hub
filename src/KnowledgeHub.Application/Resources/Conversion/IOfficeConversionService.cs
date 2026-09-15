using System.Threading;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// Office 文档预览转换服务：将 PPT/PPTX/DOC/DOCX/XLS/XLSX 转为 PDF，供前端 pdfjs 渲染。
/// 实现通常为 Gotenberg（内部基于 LibreOffice headless）。
/// 转换由媒体流水线（上传后入队）驱动，实际执行通过 ConversionConcurrencyManager 限流。
/// </summary>
public interface IOfficeConversionService
{
    /// <summary>
    /// 将指定路径的 Office 文件转为 PDF，返回 PDF 的绝对路径。
    /// 实现内部应做：缓存检查 → 并发闸门 → 转换 → 缓存写入。
    /// 同一 resourceId 的并发请求会复用同一次转换结果。
    /// </summary>
    /// <param name="resourceId">资源 ID（缓存 key）</param>
    /// <param name="sourcePath">源文件绝对路径（Office 文档）</param>
    /// <param name="serviceName">并发限流分组（默认 "preview"；后台重处理用 "reprocess"）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PDF 文件的绝对路径</returns>
    Task<string> ConvertToPdfAsync(
        string resourceId,
        string sourcePath,
        string serviceName = "preview",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除缓存的 PDF（源文件更新后调用）。
    /// </summary>
    void InvalidateCache(string resourceId);
}

/// <summary>
/// Office 转换异常。前端捕获后降级为下载原文件。
/// </summary>
public class OfficeConversionException : System.Exception
{
    public OfficeConversionException(string message) : base(message) { }
    public OfficeConversionException(string message, System.Exception inner) : base(message, inner) { }
}