using System.Threading;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// Office 文档预览转换服务：将 PPTX/DOCX/XLSX 转为 PDF，供前端 pdfjs 渲染。
/// 实现通常为 LibreOffice headless 模式。
/// </summary>
public interface IOfficeConversionService
{
    /// <summary>
    /// 将指定路径的 Office 文件转为 PDF，返回 PDF 的绝对路径。
    /// 实现内部应做：缓存检查 → 转换 → 缓存写入。
    /// 同一 resourceId 的并发请求会复用同一次转换结果。
    /// </summary>
    /// <param name="resourceId">资源 ID（缓存 key）</param>
    /// <param name="sourcePath">源文件绝对路径（PPTX/DOCX/XLSX）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PDF 文件的绝对路径</returns>
    Task<string> ConvertToPdfAsync(
        string resourceId,
        string sourcePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否已存在缓存的 PDF。
    /// </summary>
    bool HasCachedPdf(string resourceId);

    /// <summary>
    /// 清除缓存的 PDF（源文件更新后调用）。
    /// </summary>
    void InvalidateCache(string resourceId);

    /// <summary>
    /// 获取单页 PDF 的缓存路径（pdfseparate 拆分后的单个页面）。
    /// </summary>
    string GetPagePdfPath(string resourceId, int pageNumber);
}

/// <summary>
/// Office 转换异常。前端捕获后降级为下载原文件。
/// </summary>
public class OfficeConversionException : System.Exception
{
    public OfficeConversionException(string message) : base(message) { }
    public OfficeConversionException(string message, System.Exception inner) : base(message, inner) { }
}