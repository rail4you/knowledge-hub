using System.IO;

namespace KnowledgeHub.Common;

/// <summary>
/// 文件存储配置。
/// 对应配置节点: Storage（env: Storage__RootPath）。
/// RootPath 可为相对路径（相对内容根目录，默认 uploads），
/// 也可为绝对路径（容器挂载点，如 /data/uploads）。
/// 公开访问路径固定为 /uploads，不受物理路径影响。
/// </summary>
public class FileStorageOptions
{
    public string RootPath { get; set; } = "uploads";

    public string ResolveRootPath(string contentRootPath)
    {
        return Path.IsPathRooted(RootPath)
            ? RootPath
            : Path.GetFullPath(Path.Combine(contentRootPath, RootPath));
    }
}
