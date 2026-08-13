using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 转换任务队列抽象。由 Host 层用 Hangfire 实现（内存队列，不持久化）。
/// 业务代码（Controller）只依赖此接口，避免直接依赖 Hangfire。
/// </summary>
public interface IConversionTaskQueue
{
    /// <summary>
    /// 入队一个转换任务。同一资源只会入队一次（内部去重）。
    /// </summary>
    Task EnqueueAsync(string resourceId, string sourcePath);
}
