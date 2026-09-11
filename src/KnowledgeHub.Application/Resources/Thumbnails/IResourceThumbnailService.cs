using System.Threading;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.Thumbnails;

/// <summary>
/// 资源封面缩略图服务：把图片/视频源文件生成小尺寸 JPEG（最长边 maxWidth），
/// 缓存到磁盘后供列表封面使用，避免列表页下载原始大文件。
/// </summary>
public interface IResourceThumbnailService
{
    /// <summary>
    /// 获取或生成缩略图，返回缩略图绝对路径；源类型不支持或生成失败返回 null。
    /// </summary>
    /// <param name="resourceId">资源 Id（用于缓存文件名）</param>
    /// <param name="sourceFullPath">源文件绝对路径</param>
    /// <param name="maxWidth">目标最大宽度（像素）</param>
    Task<string?> GetOrCreateAsync(string resourceId, string sourceFullPath, int maxWidth, CancellationToken ct = default);
}
