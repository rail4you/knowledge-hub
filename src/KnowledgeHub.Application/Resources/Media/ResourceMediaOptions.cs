namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 遗留配置（配置节点 ResourceMedia），已合并进 <see cref="ResourceProcessingFlowOptions"/>。
/// 保留此类仅为兼容旧配置文件：Host 层仍会读取该节点并经 PostConfigure 映射到 FlowOptions，
/// 应用代码请只消费 <see cref="ResourceProcessingFlowOptions"/>。
/// </summary>
[System.Obsolete("已合并进 ResourceProcessingFlowOptions，请使用 ResourceProcessingFlow 节。")]
public class ResourceMediaOptions
{
    /// <summary>遗留：对应 FlowOptions.StartAfterIndexing。</summary>
    public bool StartAfterIndexing { get; set; }

    /// <summary>
    /// 遗留：true 表示"仅审核通过后处理"。
    /// 映射为 FlowOptions: ProcessOnUpload=false + GeneratePreviewOnPublish=false
    /// （上传/发布不做功，索引与媒体都留到院校审核后）。
    /// </summary>
    public bool GenerateOnApproval { get; set; }
}
