namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 遗留 <c>ResourceMedia</c> 配置节点到 <see cref="ResourceProcessingFlowOptions"/> 的兼容映射。
/// 由 Host 层的 PostConfigure 调用；抽成静态方法以便单元测试锁定映射语义。
/// </summary>
public static class ResourceProcessingFlowMapper
{
    /// <param name="flow">已绑定 <c>ResourceProcessingFlow</c> 节的目标配置（原地修改）。</param>
    /// <param name="legacyStartAfterIndexing">旧 <c>ResourceMedia:StartAfterIndexing</c>（null=未配置）。</param>
    /// <param name="legacyGenerateOnApproval">旧 <c>ResourceMedia:GenerateOnApproval</c>（null=未配置）。</param>
    public static void ApplyLegacy(
        ResourceProcessingFlowOptions flow,
        bool? legacyStartAfterIndexing,
        bool? legacyGenerateOnApproval)
    {
        if (legacyStartAfterIndexing == true)
        {
            flow.StartAfterIndexing = true;
        }

        if (legacyGenerateOnApproval == true)
        {
            // 旧语义"仅审核通过后处理"：上传/发布阶段不做功，索引与媒体都留到院校审核后。
            flow.ProcessOnUpload = false;
            flow.GeneratePreviewOnPublish = false;
            flow.GenerateIndexOnSchoolApproval = true;
            flow.GenerateVideoThumbnailOnSchoolApproval = true;
        }
    }
}
