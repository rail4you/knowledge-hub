using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private static readonly SemaphoreSlim Gate = new(2, 2);

    private readonly OfficeConversionOptions _options;
    private readonly ILogger<PdftoppmPageRasterizer> _logger;

    public PdftoppmPageRasterizer(
        IOptions<OfficeConversionOptions> options,
        ILogger<PdftoppmPageRasterizer> logger)
    {
        _options = options.Value;
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

        await Gate.WaitAsync(ct);
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

            if (!RunPdftoppm(args, ct))
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
        finally
        {
            Gate.Release();
        }
    }

    private bool RunPdftoppm(List<string> args, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.PdftoppmTimeoutSeconds));
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var psi = new ProcessStartInfo
        {
            FileName = _options.PdftoppmPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            _logger.LogWarning("[PdfRasterizer] 无法启动 pdftoppm: {Path}", _options.PdftoppmPath);
            return false;
        }

        var stderrTask = proc.StandardError.ReadToEndAsync(linked.Token);
        try
        {
            proc.WaitForExit((int)timeout.TotalMilliseconds);
            if (!proc.HasExited)
            {
                try { proc.Kill(true); } catch { /* ignore */ }
                return false;
            }
            if (proc.ExitCode != 0)
            {
                var err = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
                _logger.LogWarning("[PdfRasterizer] pdftoppm 退出码 {Code}: {Err}", proc.ExitCode,
                    err.Length > 300 ? err[..300] : err);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PdfRasterizer] pdftoppm 异常");
            try { proc.Kill(true); } catch { /* ignore */ }
            return false;
        }
    }
}
