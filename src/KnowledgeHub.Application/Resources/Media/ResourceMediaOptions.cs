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

    /// <summary>
    /// 是否在资源审核通过后再启动媒体处理。
    /// 默认 false：上传即生成（未通过审核的资源也会生成，浪费少量 CPU）。
    /// true：仅在审核通过（SchoolApproved）时入队，避免草稿/驳回资源白耗转换资源。
    /// </summary>
    public bool GenerateOnApproval { get; set; }
}
