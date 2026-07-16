using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    public async Task<string> ConvertToPdfAsync(
        string resourceId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new OfficeConversionException($"源文件不存在: {sourcePath}");

        // 1. 缓存命中直接返回
        var cachedPath = GetCachedPdfPath(resourceId);
        if (File.Exists(cachedPath))
        {
            _logger.LogDebug("[OfficeConversion] 缓存命中: {ResourceId} -> {Path}", resourceId, cachedPath);
            return cachedPath;
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
        // soffice 不接受中文/特殊字符文件名问题：复制到 workDir 使用 GUID 文件名
        var sourceExt = Path.GetExtension(sourcePath);
        var workSourcePath = Path.Combine(workDir, $"source{sourceExt}");

        var targetPdfPath = GetCachedPdfPath(resourceId);
        var workTargetPath = Path.Combine(workDir, "source.pdf");

        try
        {
            File.Copy(sourcePath, workSourcePath, overwrite: true);

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

    private string GetCachedPdfPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.pdf");
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