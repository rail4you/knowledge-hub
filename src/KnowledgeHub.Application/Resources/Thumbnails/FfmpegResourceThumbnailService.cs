using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Thumbnails;

/// <summary>
/// 用 ffmpeg 生成资源缩略图（图片缩放、视频抽帧），磁盘缓存 + 源文件 mtime/大小失效。
/// ffmpeg 在开发机与 API 容器镜像均可用（<see cref="OfficeConversionOptions.FfmpegPath"/>）。
/// </summary>
public class FfmpegResourceThumbnailService : IResourceThumbnailService, ISingletonDependency
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".avif"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".flv", ".mpeg", ".mpg"
    };

    /// <summary>同一资源的并发去重，避免重复触发 ffmpeg。</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PerKeyLocks = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IFileStorageService _fileStorageService;
    private readonly IFfmpegRunner _ffmpegRunner;
    private readonly ILogger<FfmpegResourceThumbnailService> _logger;

    public FfmpegResourceThumbnailService(
        IFileStorageService fileStorageService,
        IFfmpegRunner ffmpegRunner,
        ILogger<FfmpegResourceThumbnailService> logger)
    {
        _fileStorageService = fileStorageService;
        _ffmpegRunner = ffmpegRunner;
        _logger = logger;
    }

    public async Task<string?> GetOrCreateAsync(
        string resourceId,
        string sourceFullPath,
        int maxWidth,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(sourceFullPath))
            return null;
        if (!File.Exists(sourceFullPath))
            return null;

        var ext = Path.GetExtension(sourceFullPath);
        var isImage = ImageExtensions.Contains(ext);
        var isVideo = VideoExtensions.Contains(ext);
        if (!isImage && !isVideo)
            return null;

        maxWidth = Math.Clamp(maxWidth, 64, 1024);

        var dir = Path.Combine(_fileStorageService.RootPath, "thumbnails");
        Directory.CreateDirectory(dir);
        var thumbPath = Path.Combine(dir, $"{resourceId}_{maxWidth}.jpg");
        var metaPath = Path.Combine(dir, $"{resourceId}_{maxWidth}.meta");

        var info = new FileInfo(sourceFullPath);
        if (File.Exists(thumbPath) && IsCacheValid(metaPath, info))
            return thumbPath;

        var lockKey = $"{resourceId}_{maxWidth}";
        var gate = PerKeyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // 双重检查：等待期间可能已由其他请求生成
            if (File.Exists(thumbPath) && IsCacheValid(metaPath, info))
                return thumbPath;

            var result = await _ffmpegRunner.RunFfmpegAsync(BuildArgs(sourceFullPath, thumbPath, maxWidth, isVideo), ct: ct);
            if (!result.Success)
                return null;

            if (!File.Exists(thumbPath))
                return null;

            SaveMeta(metaPath, info);
            return thumbPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Thumbnail] 生成失败: {ResourceId}", resourceId);
            return null;
        }
        finally
        {
            gate.Release();
            // 用完即移除，避免字典随资源数无界增长（并发安全：即使有竞态，
            // 上面的缓存双重检查也能保证不会重复生成）。
            PerKeyLocks.TryRemove(lockKey, out _);
        }
    }

    private static List<string> BuildArgs(string input, string output, int maxWidth, bool isVideo)
    {
        var args = new List<string> { "-y" };
        if (isVideo)
        {
            // 取第 1 秒的一帧；多数视频首帧是黑屏
            args.Add("-ss");
            args.Add("1");
        }
        args.Add("-i");
        args.Add(input);
        args.Add("-vf");
        args.Add($"scale='min({maxWidth},iw)':-2");
        if (isVideo)
        {
            // 缩略图不需要音轨
            args.Add("-an");
        }
        args.Add("-frames:v");
        args.Add("1");
        args.Add("-q:v");
        args.Add("4");
        args.Add("-update");
        args.Add("1");
        args.Add("-f");
        args.Add("image2");
        args.Add(output);
        return args;
    }

    private static bool IsCacheValid(string metaPath, FileInfo source)
    {
        try
        {
            if (!File.Exists(metaPath)) return false;
            var meta = JsonSerializer.Deserialize<ThumbCacheMeta>(File.ReadAllText(metaPath));
            return meta != null
                && meta.Length == source.Length
                && meta.LastWriteTimeUtc == source.LastWriteTimeUtc;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveMeta(string metaPath, FileInfo source)
    {
        try
        {
            var meta = new ThumbCacheMeta
            {
                Length = source.Length,
                LastWriteTimeUtc = source.LastWriteTimeUtc,
            };
            File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, JsonOptions));
        }
        catch
        {
            // meta 写入失败只会导致下次重新生成，忽略
        }
    }

    private sealed class ThumbCacheMeta
    {
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
    }
}
