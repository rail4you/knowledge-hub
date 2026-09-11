using System;
using System.Collections.Generic;
using System.IO;
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
/// </summary>
[ExposeServices(typeof(IPdfPageRasterizer))]
public class PdftoppmPageRasterizer : IPdfPageRasterizer, ISingletonDependency
{
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
            var args = new List<string>
            {
                "-jpeg", "-f", "1", "-l", "1",
                "-scale-to-x", maxWidth.ToString(),
                "-scale-to-y", "-1",
                "-singlefile",
                pdfFullPath,
                prefix
            };

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
}
