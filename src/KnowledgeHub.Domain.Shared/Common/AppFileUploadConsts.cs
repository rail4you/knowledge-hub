namespace KnowledgeHub.Common;

public static class AppFileUploadConsts
{
    public const long MaxFileSize = 500 * 1024 * 1024;

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
