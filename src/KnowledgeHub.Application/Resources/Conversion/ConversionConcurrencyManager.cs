using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 每类转换服务一个动态并发闸门（可在线调整并发数，无需重启）。
/// 通过 API 接口调整某类服务（如 preview / reprocess）同时处理的任务数，
/// 默认每个服务 1 个（严格串行），防止 LibreOffice 转换打爆服务器 CPU。
/// </summary>
public class ConversionConcurrencyManager : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, ConversionGate> _gates = new();
    private readonly ILogger<ConversionConcurrencyManager> _logger;

    public ConversionConcurrencyManager(ILogger<ConversionConcurrencyManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取（或创建）某类服务的并发闸门。默认并发数 1。
    /// </summary>
    public ConversionGate GetGate(string serviceName, int defaultMaxConcurrent = 1)
    {
        return _gates.GetOrAdd(
            serviceName,
            _ => new ConversionGate(serviceName, Math.Max(1, defaultMaxConcurrent), _logger));
    }

    /// <summary>
    /// 调整某类服务的并发数（≥1）。部署后可随时放开，无需重启。
    /// </summary>
    public void SetMaxConcurrent(string serviceName, int maxConcurrent)
    {
        GetGate(serviceName).SetMaxConcurrent(Math.Max(1, maxConcurrent));
    }

    /// <summary>获取所有服务的当前并发配置（serviceName -> maxConcurrent）。</summary>
    public IReadOnlyDictionary<string, int> GetAllConfigurations()
    {
        var result = new Dictionary<string, int>();
        foreach (var kv in _gates)
        {
            result[kv.Key] = kv.Value.MaxConcurrent;
        }
        return result;
    }

    /// <summary>清除（用于测试/重置）。</summary>
    public void Reset()
    {
        foreach (var gate in _gates.Values)
        {
            gate.Dispose();
        }
        _gates.Clear();
    }
}

/// <summary>
/// 单个服务的并发闸门：动态信号量，支持在线调整 maxConcurrent（放开/收紧）。
/// 在途任务（已获取额度）在调整后仍允许完成；新任务按最新额度排队。
/// </summary>
public class ConversionGate : IDisposable
{
    private readonly object _lock = new();
    private readonly ILogger _logger;
    private readonly string _serviceName;

    // 等待队列：每个等待者在可被放行时收到信号
    private readonly Queue<TaskCompletionSource> _waiters = new();
    private int _active;          // 当前在途（已获取额度未释放）数
    private int _maxConcurrent;
    private bool _disposed;

    public string ServiceName => _serviceName;

    public int MaxConcurrent
    {
        get { lock (_lock) { return _maxConcurrent; } }
    }

    public ConversionGate(string serviceName, int maxConcurrent, ILogger logger)
    {
        _serviceName = serviceName;
        _maxConcurrent = maxConcurrent;
        _logger = logger;
    }

    /// <summary>
    /// 等待一个并发额度（受当前 maxConcurrent 限制）。
    /// 返回的 IDisposable 在业务结束后 Release()。
    /// </summary>
    public Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            if (_active < _maxConcurrent)
            {
                _active++;
                return Task.FromResult<IDisposable>(new GateReleaser(this));
            }

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Enqueue(tcs);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    lock (_lock)
                    {
                        // 移除已取消的等待者（可能已在 SetMaxConcurrent 中被移出）
                        // 简单起见：置为取消状态，WaitForResultAsync 会抛出
                    }
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return WaitForResultAsync(tcs);
        }
    }

    private async Task<IDisposable> WaitForResultAsync(TaskCompletionSource tcs)
    {
        await tcs.Task.ConfigureAwait(false);
        return new GateReleaser(this);
    }

    /// <summary>
    /// 在线调整并发数。放开（调大）立即放行排队的等待者；收紧（调小）已获取额度的
    /// 在途任务继续执行完，新任务按新额度排队。
    /// </summary>
    public void SetMaxConcurrent(int maxConcurrent)
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (maxConcurrent == _maxConcurrent) return;

            _maxConcurrent = maxConcurrent;

            // 放开：把等待者从队首放行，直到达到新额度
            while (_waiters.Count > 0 && _active < _maxConcurrent)
            {
                var tcs = _waiters.Dequeue();
                _active++;
                tcs.TrySetResult();
            }

            _logger.LogInformation(
                "[ConversionGate] {Service} 并发数调整为 {Max}",
                _serviceName, maxConcurrent);
        }
    }

    /// <summary>释放一个额度（由 GateReleaser 调用）。</summary>
    private void Release()
    {
        TaskCompletionSource? next = null;
        lock (_lock)
        {
            if (_disposed) return;
            _active--;

            // 有等待者则转交额度
            if (_waiters.Count > 0 && _active < _maxConcurrent)
            {
                next = _waiters.Dequeue();
                _active++;
            }
        }
        next?.TrySetResult();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ConversionGate));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            while (_waiters.Count > 0)
            {
                _waiters.Dequeue().TrySetCanceled();
            }
        }
    }

    private sealed class GateReleaser : IDisposable
    {
        private readonly ConversionGate _gate;
        private int _released;

        public GateReleaser(ConversionGate gate) => _gate = gate;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _gate.Release();
            }
        }
    }
}
