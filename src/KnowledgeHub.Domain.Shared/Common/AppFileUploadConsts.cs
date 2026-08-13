namespace KnowledgeHub.Common;

public static class AppFileUploadConsts
{
    /// <summary>
    /// 上传文件大小上限（100MB）。
    /// 超大文件（如 200MB PPTX）上传后在线 PDF 预览会长期占满服务器 CPU/内存，
    /// 故上传环节即限制，避免超大数据进入系统。
    /// </summary>
    public const long MaxFileSize = 100 * 1024 * 1024;

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
