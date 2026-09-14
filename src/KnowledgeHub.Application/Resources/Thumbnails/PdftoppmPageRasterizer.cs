using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Conversion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Thumbnails;

/// <summary>
/// PDF 首页光栅化：把 PDF 第 1 页渲染成 JPEG 缩略图（用于 Office/PDF 封面）。
/// </summary>
public interface IPdfPageRasterizer
{
    /// <summary>渲染 PDF 第 1 页为 outputJpegPath；失败返回 null。</summary>
    Task<string?> RasterizeFirstPageAsync(string pdfFullPath, string outputJpegPath, int maxWidth, CancellationToken ct = default);
}

/// <summary>
/// 基于 poppler `pdftoppm` 的实现。并发限流 2，命令缺失/失败时优雅返回 null。
/// 当首页内容明显偏小（如稀疏的 Excel 表格）时，用 `pdftotext -bbox` 探测内容边界并裁剪，
/// 避免封面是一大片空白。
/// </summary>
[ExposeServices(typeof(IPdfPageRasterizer))]
public class PdftoppmPageRasterizer : IPdfPageRasterizer, ISingletonDependency
{
    private static readonly Regex PageSizeRegex =
        new("<page\\s+width=\"([0-9.]+)\"\\s+height=\"([0-9.]+)\"", RegexOptions.Compiled);

    private static readonly Regex WordBoxRegex =
        new("xMin=\"([0-9.]+)\"\\s+yMin=\"([0-9.]+)\"\\s+xMax=\"([0-9.]+)\"\\s+yMax=\"([0-9.]+)\"", RegexOptions.Compiled);

    private readonly OfficeConversionOptions _options;
    private readonly IFfmpegRunner _runner;
    private readonly ILogger<PdftoppmPageRasterizer> _logger;

    public PdftoppmPageRasterizer(
        IOptions<OfficeConversionOptions> options,
        IFfmpegRunner runner,
        ILogger<PdftoppmPageRasterizer> logger)
    {
        _options = options.Value;
        _runner = runner;
        _logger = logger;
    }

    public async Task<string?> RasterizeFirstPageAsync(
        string pdfFullPath,
        string outputJpegPath,
        int maxWidth,
        CancellationToken ct = default)
    {
        if (!File.Exists(pdfFullPath))
        {
            return null;
        }

        maxWidth = Math.Clamp(maxWidth, 64, 1600);
        var dir = Path.GetDirectoryName(outputJpegPath);
        if (string.IsNullOrEmpty(dir))
        {
            return null;
        }
        Directory.CreateDirectory(dir);

        if (File.Exists(outputJpegPath))
        {
            return outputJpegPath;
        }

        try
        {
            var prefix = Path.Combine(dir, Path.GetFileNameWithoutExtension(outputJpegPath));
            var args = await BuildArgsAsync(pdfFullPath, maxWidth, prefix, ct);
            var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.PdftoppmTimeoutSeconds));
            var result = await _runner.RunAsync(_options.PdftoppmPath, args, timeout, ct);
            if (!result.Success)
            {
                return null;
            }

            return File.Exists(outputJpegPath) ? outputJpegPath : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PdfRasterizer] 首页渲染失败: {Pdf}", pdfFullPath);
            return null;
        }
    }

    private async Task<List<string>> BuildArgsAsync(string pdf, int maxWidth, string prefix, CancellationToken ct)
    {
        var args = new List<string> { "-jpeg", "-f", "1", "-l", "1" };

        var crop = await TryGetContentCropAsync(pdf, maxWidth, ct);
        if (crop != null)
        {
            // 按内容边界裁剪：以 dpi 控制输出宽度 ≈ maxWidth，避免整页空白
            args.Add("-r");
            args.Add(crop.Value.Dpi.ToString(CultureInfo.InvariantCulture));
            args.Add("-x");
            args.Add(crop.Value.X.ToString(CultureInfo.InvariantCulture));
            args.Add("-y");
            args.Add(crop.Value.Y.ToString(CultureInfo.InvariantCulture));
            args.Add("-W");
            args.Add(crop.Value.W.ToString(CultureInfo.InvariantCulture));
            args.Add("-H");
            args.Add(crop.Value.H.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            // 常规整页渲染（矢量缩放到目标宽度，最清晰）
            args.Add("-scale-to-x");
            args.Add(maxWidth.ToString(CultureInfo.InvariantCulture));
            args.Add("-scale-to-y");
            args.Add("-1");
        }

        args.Add("-singlefile");
        args.Add(pdf);
        args.Add(prefix);
        return args;
    }

    /// <summary>
    /// 用 `pdftotext -bbox` 探测首页文本边界，只有当内容明显偏小（大面积留白，如稀疏表格）时
    /// 才返回裁剪区域；否则返回 null 走整页渲染，避免裁掉图片/图表元素。
    /// </summary>
    private async Task<(int X, int Y, int W, int H, int Dpi)?> TryGetContentCropAsync(
        string pdf, int maxWidth, CancellationToken ct)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bbox-{Guid.NewGuid():N}.html");
        try
        {
            var args = new List<string> { "-bbox", "-f", "1", "-l", "1", pdf, tmp };
            var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.PdftoppmTimeoutSeconds));
            var result = await _runner.RunAsync(_options.PdftotextPath, args, timeout, ct);
            if (!result.Success || !File.Exists(tmp))
            {
                return null;
            }

            var html = await File.ReadAllTextAsync(tmp, ct);

            var page = PageSizeRegex.Match(html);
            if (!page.Success)
            {
                return null;
            }
            var pageW = double.Parse(page.Groups[1].Value, CultureInfo.InvariantCulture);
            var pageH = double.Parse(page.Groups[2].Value, CultureInfo.InvariantCulture);
            if (pageW <= 0 || pageH <= 0)
            {
                return null;
            }

            double xMin = double.MaxValue, yMin = double.MaxValue, xMax = double.MinValue, yMax = double.MinValue;
            var any = false;
            foreach (Match m in WordBoxRegex.Matches(html))
            {
                var x1 = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var y1 = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var x2 = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                var y2 = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
                xMin = Math.Min(xMin, x1);
                yMin = Math.Min(yMin, y1);
                xMax = Math.Max(xMax, x2);
                yMax = Math.Max(yMax, y2);
                any = true;
            }
            if (!any)
            {
                return null;
            }

            var contentW = xMax - xMin;
            var contentH = yMax - yMin;
            // 仅当宽、高都明显小于整页时才裁剪（内容挤在一角）；否则整页渲染。
            if (!(contentW < 0.6 * pageW && contentH < 0.6 * pageH))
            {
                return null;
            }

            var margin = Math.Clamp(0.08 * Math.Max(contentW, contentH), 4, 24);
            var x0 = Math.Max(0, xMin - margin);
            var y0 = Math.Max(0, yMin - margin);
            var w0 = Math.Min(pageW - x0, contentW + 2 * margin);
            var h0 = Math.Min(pageH - y0, contentH + 2 * margin);
            if (w0 <= 1 || h0 <= 1)
            {
                return null;
            }

            // 让裁剪后的输出宽度约等于 maxWidth
            var dpi = Math.Clamp(72.0 * maxWidth / w0, 72, 1200);
            var sx = dpi / 72.0;
            var x = Math.Max(0, (int)Math.Floor(x0 * sx));
            var y = Math.Max(0, (int)Math.Floor(y0 * sx));
            var w = Math.Max(1, (int)Math.Ceiling(w0 * sx));
            var h = Math.Max(1, (int)Math.Ceiling(h0 * sx));
            return (x, y, w, h, (int)Math.Round(dpi));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[PdfRasterizer] 内容边界探测失败，回退整页渲染: {Pdf}", pdf);
            return null;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
        }
    }
}
