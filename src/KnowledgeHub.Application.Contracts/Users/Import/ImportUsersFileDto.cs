namespace KnowledgeHub.Users;

/// <summary>
/// 用户批量导入文件输入：xlsx 文件内容的 Base64 编码。
/// 说明：ASP.NET Core 默认 JSON 序列化（System.Text.Json）无法把数字数组反序列化为 byte[]，
/// 只能接受 Base64 字符串，因此导入接口统一用本 DTO 传文件（与就业模块 ImportJobsInput 一致）。
/// </summary>
public class ImportUsersFileDto
{
    /// <summary>xlsx 文件内容的 Base64 编码</summary>
    public string FileBase64 { get; set; } = string.Empty;

    /// <summary>原始文件名（仅用于提示，可选）</summary>
    public string? FileName { get; set; }
}
