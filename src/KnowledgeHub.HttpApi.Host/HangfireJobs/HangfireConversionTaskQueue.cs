using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.Resources.Conversion;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 实现的转换任务队列（内存存储，不持久化，重启清空）。
/// 同一资源去重：转换进行中不再重复 enqueue。
/// </summary>
public class HangfireConversionTaskQueue : IConversionTaskQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IOfficeConversionService _officeConversionService;
    private readonly ILogger<HangfireConversionTaskQueue> _logger;
    private readonly ConcurrentDictionary<string, byte> _queued = new();

    public HangfireConversionTaskQueue(
        IBackgroundJobClient backgroundJobClient,
        IOfficeConversionService officeConversionService,
        ILogger<HangfireConversionTaskQueue> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _officeConversionService = officeConversionService;
        _logger = logger;
    }

    public Task EnqueueAsync(string resourceId, string sourcePath)
    {
        // 已在排队/执行中（Hangfire job 未完成 or ConvertToPdfAsync in-flight）则跳过
        if (!_queued.TryAdd(resourceId, 0))
        {
            return Task.CompletedTask;
        }

        if (_officeConversionService.IsInFlight(resourceId) ||
            _officeConversionService.HasValidCachedPdf(resourceId, sourcePath))
        {
            _queued.TryRemove(resourceId, out _);
            return Task.CompletedTask;
        }

        _logger.LogInformation("[ConversionQueue] 入队转换: {ResourceId}", resourceId);

        _backgroundJobClient.Enqueue<OfficeConversionJob>(
            "conversion",
            job => job.ConvertAsync(resourceId, sourcePath));

        // job 完成后移除去重标记（job 内部无法直接通知这里，用延时清理兜底；
        // 实际在 IsInFlight 判断 + ConvertToPdfAsync 内部缓存/in-flight 去重已足够安全）。
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(30));
            _queued.TryRemove(resourceId, out _);
        });

        return Task.CompletedTask;
    }
}
