using System.Threading;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// HTML → PDF（打印用）。走 Gotenberg 的 Chromium 模块（<c>/forms/chromium/convert/html</c>）。
/// <para>
/// 与 <see cref="IOfficeConversionService"/> 的区别：那条是「Office 文档预览」，带缓存与并发闸门；
/// 本条是「即时排版打印」，每次内容都可能不同，不缓存。
/// </para>
/// <para>
/// 中文字体依赖 Gotenberg 镜像：官方 <c>gotenberg/gotenberg:8</c> 已内置 Noto CJK；
/// 若换成精简镜像出现方块字，需要按 <c>etc/docker/Dockerfile.gotenberg</c> 那样装中文字体。
/// </para>
/// </summary>
public interface IHtmlToPdfService
{
    /// <summary>
    /// 把 HTML 渲染成 PDF 字节。A4 纵向、边距 12mm（贴合打印/导出习惯）。
    /// </summary>
    Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default);
}
