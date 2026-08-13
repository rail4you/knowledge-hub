namespace KnowledgeHub.Common;

public static class AppFileUploadConsts
{
    /// <summary>
    /// 上传文件大小上限默认值（500MB）。
    /// 实际限制由配置 App:MaxFileSizeBytes 决定（默认 500MB），
    /// 运行期可通过 App__MaxFileSizeBytes 环境变量覆盖。
    /// 超大 PPTX 上传后由 PptxImagePreprocessor 预压缩，不再需要 100MB 硬限制。
    /// </summary>
    public const long MaxFileSize = 500L * 1024 * 1024;

    public const int ChunkSize = 2 * 1024 * 1024;

    public static readonly string[] AllowedExtensions =
    {
        ".doc", ".docx", ".pdf",
        ".mp4", ".avi",
        ".mp3",
        ".jpg", ".jpeg", ".png",
        ".ppt", ".pptx"
    };

    public static readonly string[] DocumentExtensions = { ".doc", ".docx", ".pdf" };
    public static readonly string[] VideoExtensions = { ".mp4", ".avi" };
    public static readonly string[] AudioExtensions = { ".mp3" };
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png" };
    public static readonly string[] PptExtensions = { ".ppt", ".pptx" };
}
