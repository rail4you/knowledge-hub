namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源处理流水线触发时机配置（配置节点 ResourceProcessingFlow）。
///
/// 目标流程：
///   上传            → 草稿（不生成任何后台任务）
///   发布/提交审核    → 生成预览（缩略图 / 预览 PDF），供审核人查看
///   院校审核通过     → 启动索引；视频缩略图随院校审核一并生成
///   联盟审核通过     → 学生端可见
///
/// 默认全部开启新流程；如需回退旧的"上传即处理"行为，把 ProcessOnUpload 置为 true。
/// </summary>
public class ResourceProcessingFlowOptions
{
    /// <summary>
    /// 上传时立即处理（兼容旧行为）。
    /// 默认 false：上传仅存草稿，不生成索引/媒体任务。
    /// </summary>
    public bool ProcessOnUpload { get; set; }

    /// <summary>
    /// 发布/提交审核后生成预览（缩略图 + 预览 PDF）。默认 true。
    /// </summary>
    public bool GeneratePreviewOnPublish { get; set; } = true;

    /// <summary>
    /// 院校审核通过后启动索引任务（文档解析 / 视频时间轴）。默认 true。
    /// </summary>
    public bool GenerateIndexOnSchoolApproval { get; set; } = true;

    /// <summary>
    /// 视频缩略图延后到院校审核通过时生成（发布阶段不为视频生成媒体任务）。默认 true。
    /// </summary>
    public bool GenerateVideoThumbnailOnSchoolApproval { get; set; } = true;
}
