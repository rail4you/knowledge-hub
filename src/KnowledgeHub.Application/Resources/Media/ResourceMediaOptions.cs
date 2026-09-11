namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源媒体处理配置（配置节点 ResourceMedia）。
/// </summary>
public class ResourceMediaOptions
{
    /// <summary>
    /// 是否在索引任务完成后再启动媒体处理。
    /// 默认 false：媒体处理与索引并行入队（互不阻塞）。
    /// true：等待索引 Completed 后再入队媒体任务，降低大文件并发压力。
    /// </summary>
    public bool StartAfterIndexing { get; set; }
}
