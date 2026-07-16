using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 使用 LibreOffice headless 模式将 Office 文档转为 PDF。
/// 转换结果缓存到 FileStorage/converted/{resourceId}.pdf，避免重复转换。
/// </summary>
public class LibreOfficeConversionService : IOfficeConversionService, ITransientDependency
{
    private readonly IFileStorageService _fileStorageService;
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<LibreOfficeConversionService> _logger;

    /// <summary>
    /// 并发限流：防止多用户同时转换打爆 CPU。
    /// 超出最大并发数时排队等待。
    /// </summary>
    private readonly SemaphoreSlim _concurrencyLimiter;

    /// <summary>
    /// 同一 resourceId 的并发请求复用同一次转换。
    /// key: resourceId, value: Lazy<Task<string>>
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new();

    public LibreOfficeConversionService(
        IFileStorageService fileStorageService,
        IOptions<OfficeConversionOptions> options,
        ILogger<LibreOfficeConversionService> logger)
    {
        _fileStorageService = fileStorageService;
        _options = options.Value;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrentConversions);
    }

    public bool HasCachedPdf(string resourceId)
    {
        var path = GetCachedPdfPath(resourceId);
        return File.Exists(path);
    }

    public void InvalidateCache(string resourceId)
    {
        var path = GetCachedPdfPath(resourceId);
        var metaPath = GetCacheMetaPath(resourceId);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("[OfficeConversion] 缓存已清除: {Path}", path);
            }
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OfficeConversion] 清除缓存失败: {Path}", path);
        }
    }

    public async Task<string> ConvertToPdfAsync(
        string resourceId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new OfficeConversionException($"源文件不存在: {sourcePath}");

        // 1. 检查缓存是否有效（对比源文件修改时间）
        var cachedPath = GetCachedPdfPath(resourceId);
        if (File.Exists(cachedPath) && IsCacheValid(resourceId, sourcePath))
        {
            _logger.LogDebug("[OfficeConversion] 缓存命中: {ResourceId} -> {Path}", resourceId, cachedPath);
            return cachedPath;
        }

        if (File.Exists(cachedPath))
        {
            _logger.LogInformation("[OfficeConversion] 缓存已过期（源文件已变化），重新转换: {ResourceId}", resourceId);
            InvalidateCache(resourceId);
        }

        // 2. 同一资源的并发请求复用同一次 Task（避免双转换）
        var lazy = _inflight.GetOrAdd(
            resourceId,
            id => new Lazy<Task<string>>(() => DoConvertAsync(id, sourcePath, cancellationToken)));

        try
        {
            return await lazy.Value;
        }
        finally
        {
            // 任务完成后从 inflight 字典移除（不影响已完成缓存的命中）
            _inflight.TryRemove(resourceId, out _);
        }
    }

    private async Task<string> DoConvertAsync(
        string resourceId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            return await RunSofficeAsync(resourceId, sourcePath, cancellationToken);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task<string> RunSofficeAsync(
        string resourceId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        // 准备临时工作目录
        var workDir = Path.Combine(Path.GetTempPath(), $"lo-{resourceId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        var sourceExt = Path.GetExtension(sourcePath)?.ToLowerInvariant();
        var workSourcePath = Path.Combine(workDir, $"source{sourceExt}");

        var targetPdfPath = GetCachedPdfPath(resourceId);
        var workTargetPath = Path.Combine(workDir, "source.pdf");

        try
        {
            // PPTX: 预处理，去掉隐藏幻灯片标记，确保 LibreOffice 导出全部幻灯片
            var actualSourcePath = sourceExt == ".pptx" || sourceExt == ".ppt"
                ? PreparePptxWithAllSlidesVisible(sourcePath, workDir)
                : sourcePath;

            if (actualSourcePath != sourcePath)
            {
                // 预处理后重命名为 source.pptx，确保 soffice 输出 source.pdf
                File.Copy(actualSourcePath, workSourcePath, overwrite: true);
            }
            else
            {
                File.Copy(sourcePath, workSourcePath, overwrite: true);
            }

            var args = $"--headless --norestore --nofirststartwizard --nologo --nolockcheck" +
                       $" --convert-to pdf --outdir \"{workDir}\" \"{workSourcePath}\"";

            _logger.LogInformation(
                "[OfficeConversion] 开始转换: {ResourceId}, cmd: {Cmd} {Args}",
                resourceId, _options.SofficePath, args);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.SofficePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir,
                },
            };

            // 设置环境变量避免 LibreOffice 写锁冲突
            process.StartInfo.EnvironmentVariables["HOME"] = workDir;

            var sw = Stopwatch.StartNew();
            if (!process.Start())
                throw new OfficeConversionException("无法启动 soffice 进程");

            // 异步读取输出，防止 stdout/stderr 缓冲区满导致进程阻塞
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // 等待进程退出或超时
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.ConversionTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                if (timeoutCts.IsCancellationRequested)
                    throw new OfficeConversionException(
                        $"Office 转换超时（{_options.ConversionTimeoutSeconds}s），文件可能过大或包含复杂内容");
                throw;
            }

            sw.Stop();
            var stderr = await stderrTask;
            var stdout = await stdoutTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "[OfficeConversion] soffice 退出码非零: {Code}, stderr: {Stderr}",
                    process.ExitCode, stderr);
                throw new OfficeConversionException(
                    $"soffice 转换失败（exit={process.ExitCode}）: {Truncate(stderr, 500)}");
            }

            if (!File.Exists(workTargetPath))
            {
                _logger.LogError(
                    "[OfficeConversion] 输出文件不存在: {Path}, stderr: {Stderr}, stdout: {Stdout}",
                    workTargetPath, stderr, stdout);
                throw new OfficeConversionException("soffice 转换完成但未生成 PDF 文件");
            }

            // 移动到缓存目录
            Directory.CreateDirectory(Path.GetDirectoryName(targetPdfPath)!);
            File.Copy(workTargetPath, targetPdfPath, overwrite: true);
            SaveCacheMeta(resourceId, sourcePath);

            // 拆分成单页 PDF，加速首页加载
            await SplitPdfToPagesAsync(resourceId, targetPdfPath);

            _logger.LogInformation(
                "[OfficeConversion] 转换成功: {ResourceId}, 耗时 {Elapsed}ms, 大小 {Size}",
                resourceId, sw.ElapsedMilliseconds, new FileInfo(targetPdfPath).Length);

            return targetPdfPath;
        }
        finally
        {
            // 清理临时工作目录
            try
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, recursive: true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// PPTX 预处理：去掉所有隐藏幻灯片标记 (show="0")，
    /// 确保 LibreOffice 导出全部幻灯片。返回处理后的文件路径。
    /// </summary>
    private string PreparePptxWithAllSlidesVisible(string sourcePath, string workDir)
    {
        var preppedPath = Path.Combine(workDir, "prepped.pptx");
        try
        {
            using var sourceZip = ZipFile.OpenRead(sourcePath);
            using var targetZip = ZipFile.Open(preppedPath, ZipArchiveMode.Create);

            var hasHiddenSlides = false;

            foreach (var entry in sourceZip.Entries)
            {
                if (entry.FullName.StartsWith("ppt/slides/slide") &&
                    entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    var doc = XDocument.Load(stream);
                    XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
                    var sld = doc.Root;
                    if (sld != null)
                    {
                        var showAttr = sld.Attribute("show");
                        if (showAttr != null && showAttr.Value == "0")
                        {
                            showAttr.Remove();
                            hasHiddenSlides = true;
                        }
                    }

                    var newEntry = targetZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var newStream = newEntry.Open();
                    doc.Save(newStream);
                }
                else
                {
                    // 直接复制其他条目
                    var newEntry = targetZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var sourceStream = entry.Open();
                    using var targetStream = newEntry.Open();
                    sourceStream.CopyTo(targetStream);
                }
            }

            if (hasHiddenSlides)
            {
                _logger.LogInformation(
                    "[OfficeConversion] 已移除 PPTX 隐藏幻灯片标记: {Source} -> {Prepped}",
                    Path.GetFileName(sourcePath), preppedPath);
                return preppedPath;
            }

            // 没有隐藏幻灯片，删除预处理文件，使用原始文件
            try { File.Delete(preppedPath); } catch { /* ignore */ }
            return sourcePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OfficeConversion] PPTX 预处理失败，使用原始文件");
            try { if (File.Exists(preppedPath)) File.Delete(preppedPath); } catch { /* ignore */ }
            return sourcePath;
        }
    }

    private string GetCachedPdfPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.pdf");
    }

    public string GetPagePdfPath(string resourceId, int pageNumber)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}",
            $"page{pageNumber}.pdf");
    }

    /// <summary>
    /// 用 PdfSharp 将完整 PDF 拆分为单页 PDF，缓存到 {resourceId}/page{N}.pdf。
    /// 每页仅 ~100-300KB，首页秒出。
    /// </summary>
    private async Task SplitPdfToPagesAsync(string resourceId, string pdfPath)
    {
        var pagesDir = Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            resourceId);

        // 检查是否已拆分
        if (Directory.Exists(pagesDir) && Directory.GetFiles(pagesDir, "page*.pdf").Length > 0)
        {
            _logger.LogDebug("[OfficeConversion] 页面缓存已存在: {ResourceId}", resourceId);
            return;
        }

        // 原子性：先写到 tmp 目录，全部成功后才 rename，避免中途失败留下半截 page*.pdf
        // 被"目录已存在 + 有 page*.pdf"的跳过逻辑误判为完成
        var tmpDir = pagesDir + ".tmp-" + Guid.NewGuid().ToString("N")[..8];

        try
        {
            Directory.CreateDirectory(tmpDir);

            var sw = Stopwatch.StartNew();
            int pageCount;
            using (var source = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
            {
                if (source.PageCount == 0)
                    throw new OfficeConversionException("PDF 无页面: " + pdfPath);

                pageCount = source.PageCount;
                for (var i = 0; i < pageCount; i++)
                {
                    using var target = new PdfDocument();
                    target.Version = source.Version;
                    target.AddPage(source.Pages[i]);
                    target.Save(Path.Combine(tmpDir, $"page{i + 1}.pdf"));
                }
            }
            sw.Stop();

            // 替换旧目录（如果存在）
            if (Directory.Exists(pagesDir))
                Directory.Delete(pagesDir, recursive: true);
            Directory.Move(tmpDir, pagesDir);

            // 边车文件：记录页数，供 /preview-pdf-info 端点 O(1) 查询，
            // 避免前端轮询每页 + 后端重复打开 PDF
            await File.WriteAllTextAsync(
                Path.Combine(pagesDir, ".count"),
                pageCount.ToString());
            _logger.LogInformation(
                "[OfficeConversion] PDF 拆分为 {Count} 页: {ResourceId}, 耗时 {Elapsed}ms",
                pageCount, resourceId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // 失败时清理 tmpDir，抛异常让上层感知
            try
            {
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir, recursive: true);
            }
            catch
            {
                // ignore cleanup errors
            }

            _logger.LogError(ex, "[OfficeConversion] PDF 拆分失败: {ResourceId}", resourceId);
            throw new OfficeConversionException("PDF 拆分失败: " + ex.Message, ex);
        }
    }

    private string GetCacheMetaPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.meta");
    }

    /// <summary>
    /// 通过源文件的 LastWriteTime + Length 对比来判断缓存是否有效。
    /// 源文件被替换（re-upload / new version）时自动失效。
    /// </summary>
    private bool IsCacheValid(string resourceId, string sourcePath)
    {
        var metaPath = GetCacheMetaPath(resourceId);
        if (!File.Exists(metaPath)) return false;

        try
        {
            var metaJson = File.ReadAllText(metaPath);
            var meta = System.Text.Json.JsonSerializer.Deserialize<CacheMeta>(metaJson);
            if (meta == null) return false;

            var sourceInfo = new FileInfo(sourcePath);
            return meta.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc
                && meta.Length == sourceInfo.Length;
        }
        catch
        {
            return false;
        }
    }

    private void SaveCacheMeta(string resourceId, string sourcePath)
    {
        try
        {
            var sourceInfo = new FileInfo(sourcePath);
            var meta = new CacheMeta
            {
                LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc,
                Length = sourceInfo.Length
            };
            var metaPath = GetCacheMetaPath(resourceId);
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
            File.WriteAllText(metaPath,
                System.Text.Json.JsonSerializer.Serialize(meta));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OfficeConversion] 保存缓存元数据失败: {ResourceId}", resourceId);
        }
    }

    private class CacheMeta
    {
        public DateTime LastWriteTimeUtc { get; set; }
        public long Length { get; set; }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";
}