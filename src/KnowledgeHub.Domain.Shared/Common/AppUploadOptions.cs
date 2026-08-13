using Microsoft.Extensions.Configuration;

namespace KnowledgeHub.Common;

/// <summary>
/// 上传文件大小限制配置。
/// 对应配置节点: App:MaxFileSizeBytes（env: App__MaxFileSizeBytes）。
/// 默认 500MB：超大 PPTX（200MB+）上传后已由 PptxImagePreprocessor 预压缩，
/// 不再需要在上传环节用 100MB 硬限制拦截。
/// </summary>
public class AppUploadOptions
{
    public long MaxFileSizeBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>
    /// 从 IConfiguration 绑定（兼容 Program.cs 在 DI 就绪前读取）。
    /// </summary>
    public static AppUploadOptions FromConfiguration(IConfiguration configuration)
    {
        return new AppUploadOptions
        {
            MaxFileSizeBytes = configuration.GetValue<long>(
                "App:MaxFileSizeBytes", 500L * 1024 * 1024)
        };
    }
}
