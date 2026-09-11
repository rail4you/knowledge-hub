using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>外部进程执行结果。</summary>
public sealed record ProcessRunResult(bool Success, int ExitCode, string StandardError)
{
    public static ProcessRunResult Fail(string error, int exitCode = -1) => new(false, exitCode, error);
}

/// <summary>
/// 统一的子进程执行器：异步等待、全局并发上限、超时与取消。
/// 供 ffmpeg / pdftoppm 等重型进程复用，避免各处重复实现
/// 且防止并发进程打爆 CPU（本机 2 核）。
/// </summary>
public interface IFfmpegRunner
{
    /// <summary>执行 ffmpeg（可执行文件与默认超时取自 <see cref="OfficeConversionOptions"/>）。</summary>
    Task<ProcessRunResult> RunFfmpegAsync(
        IReadOnlyList<string> args,
        TimeSpan? timeout = null,
        CancellationToken ct = default);

    /// <summary>执行任意外部进程（如 pdftoppm）。</summary>
    Task<ProcessRunResult> RunAsync(
        string executable,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct = default);
}

[ExposeServices(typeof(IFfmpegRunner))]
public class FfmpegRunner : IFfmpegRunner, ISingletonDependency
{
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<FfmpegRunner> _logger;
    private readonly SemaphoreSlim _gate;

    public FfmpegRunner(IOptions<OfficeConversionOptions> options, ILogger<FfmpegRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
        var max = Math.Max(1, _options.FfmpegMaxConcurrency);
        _gate = new SemaphoreSlim(max, max);
    }

    public Task<ProcessRunResult> RunFfmpegAsync(
        IReadOnlyList<string> args,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var effective = timeout ?? TimeSpan.FromSeconds(Math.Max(1, _options.FfmpegTimeoutSeconds));
        return RunAsync(_options.FfmpegPath, args, effective, ct);
    }

    public async Task<ProcessRunResult> RunAsync(
        string executable,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogWarning("[FfmpegRunner] 无法启动进程: {Executable}", executable);
                return ProcessRunResult.Fail("cannot start process");
            }

            // 必须并行消费输出，否则管道写满会阻塞子进程导致假死。
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            try
            {
                await proc.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                var reason = timeoutCts.IsCancellationRequested ? "timeout" : "cancelled";
                _logger.LogWarning("[FfmpegRunner] {Executable} {Reason} ({Timeout}s)", executable, reason, timeout.TotalSeconds);
                return ProcessRunResult.Fail(reason);
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                _logger.LogWarning("[FfmpegRunner] {Executable} 退出码 {Code}: {Err}",
                    executable, proc.ExitCode, Truncate(stderr, 500));
                return ProcessRunResult.Fail(stderr, proc.ExitCode);
            }

            return new ProcessRunResult(true, 0, stderr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FfmpegRunner] {Executable} 执行异常", executable);
            return ProcessRunResult.Fail(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}
