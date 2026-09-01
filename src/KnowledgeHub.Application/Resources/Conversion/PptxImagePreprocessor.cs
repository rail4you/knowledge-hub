using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// PPTX 大媒体预压缩：在交给 LibreOffice/Gotenberg 之前，先把包内的大图
/// （GIF/PNG/JPEG）用 ffmpeg 压小，显著降低 LibreOffice 转换时的内存峰值。
///
/// 背景：LibreOffice 加载 PPTX 时会把整个对象模型（含全部媒体）建进内存，
/// Gotenberg 的 reduceImageResolution 只能减小输出 PDF，无法省掉加载内存。
/// 所以必须在文件层面把大图物理压小（GIF 取中间帧转 JPEG），让 LibreOffice
/// 每次加载的就是小文件。
///
/// 处理方式（纯 zip 包操作，不引入 python-pptx）：
///   1. 枚举 ppt/media/*，找出超过阈值的大图；
///   2. 每个大图 ffmpeg 抽帧/缩放转 JPEG；
///   3. 重写引用这些媒体的所有 .rels、[Content_Types].xml、presentation.xml；
///   4. 可选剥离嵌入字体（由 LibreOffice Noto CJK 兜底渲染）。
/// 产物缓存到 converted/{resourceId}.light.pptx，源文件变化时自动失效。
/// </summary>
public class PptxImagePreprocessor : ISingletonDependency
{
    private readonly IFileStorageService _fileStorageService;
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<PptxImagePreprocessor> _logger;

    public PptxImagePreprocessor(
        IFileStorageService fileStorageService,
        IOptions<OfficeConversionOptions> options,
        ILogger<PptxImagePreprocessor> logger)
    {
        _fileStorageService = fileStorageService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 该源文件是否需要预压缩（PPTX + 超过阈值 + 启用开关）。
    /// </summary>
    public bool ShouldPreprocess(string sourcePath)
    {
        if (!_options.EnablePptxPreprocess) return false;
        if (!string.Equals(Path.GetExtension(sourcePath), ".pptx", StringComparison.OrdinalIgnoreCase))
            return false;
        return new FileInfo(sourcePath).Length > _options.PptxPreprocessThresholdBytes;
    }

    public string GetLightPptxPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.light.pptx");
    }

    private string GetLightMetaPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.light.meta");
    }

    private bool IsCacheValid(string metaPath, string sourcePath)
    {
        if (!File.Exists(metaPath) || !File.Exists(sourcePath)) return false;
        try
        {
            var meta = System.Text.Json.JsonSerializer.Deserialize<CacheMeta>(File.ReadAllText(metaPath));
            var info = new FileInfo(sourcePath);
            return meta != null
                && meta.LastWriteTimeUtc == info.LastWriteTimeUtc
                && meta.Length == info.Length;
        }
        catch
        {
            return false;
        }
    }

    private void SaveMeta(string metaPath, string sourcePath)
    {
        try
        {
            var info = new FileInfo(sourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
            File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(new CacheMeta
            {
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                Length = info.Length
            }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PptxPreprocess] 保存元数据失败: {Path}", metaPath);
        }
    }

    private sealed class CacheMeta
    {
        public DateTime LastWriteTimeUtc { get; set; }
        public long Length { get; set; }
    }

    /// <summary>
    /// 获取（或生成）预压缩后的 light PPTX 路径。缓存有效则直接返回。
    /// 生成失败时返回 null（调用方应回退到原始文件）。
    /// </summary>
    public Task<string?> GetOrCreateLightAsync(string resourceId, string sourcePath, CancellationToken ct = default)
    {
        var lightPath = GetLightPptxPath(resourceId);
        var metaPath = GetLightMetaPath(resourceId);

        if (File.Exists(lightPath) && IsCacheValid(metaPath, sourcePath))
        {
            return Task.FromResult<string?>(lightPath);
        }

        // 无效缓存先清理，避免并发读到旧文件
        TryDelete(lightPath);
        TryDelete(metaPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lightPath)!);
            if (Preprocess(sourcePath, lightPath, ct))
            {
                SaveMeta(metaPath, sourcePath);
                return Task.FromResult<string?>(lightPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PptxPreprocess] 预压缩失败: {Source}", sourcePath);
            TryDelete(lightPath);
        }

        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// 同步判断 light PPTX 缓存是否对应当前源文件（供 HasValidCachedPdf 使用）。
    /// 不存在/已失效返回 false。
    /// </summary>
    public bool HasValidLight(string resourceId, string sourcePath)
    {
        var lightPath = GetLightPptxPath(resourceId);
        if (!File.Exists(lightPath)) return false;
        return IsCacheValid(GetLightMetaPath(resourceId), sourcePath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// 预压缩主流程。返回是否成功。
    /// </summary>
    private bool Preprocess(string sourcePath, string outPath, CancellationToken ct)
    {
        var workDir = Path.Combine(
            Path.GetTempPath(),
            $"pptxpre-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            using var sourceZip = ZipFile.OpenRead(sourcePath);
            var entries = sourceZip.Entries;
            var mediaNames = entries.Select(e => e.FullName)
                .Where(n => n.StartsWith("ppt/media/", StringComparison.OrdinalIgnoreCase))
                .Where(n => !n.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 找出超过阈值的大媒体，逐一 ffmpeg 压缩
            var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var compressedBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in mediaNames)
            {
                var entry = sourceZip.GetEntry(name);
                if (entry == null || entry.Length <= _options.PptxMediaCompressThresholdBytes)
                    continue;

                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (ext is not (".gif" or ".png" or ".jpg" or ".jpeg"))
                    continue;

                var baseName = Path.GetFileName(name);
                var stem = Path.GetFileNameWithoutExtension(baseName);
                // 统一输出 JPEG（GIF 抽帧、静态图缩放都转 JPEG，LibreOffice 渲染一致）
                var outBase = $"{stem}.preview.jpg";

                var inputPath = Path.Combine(workDir, baseName);
                using (var fs = File.Create(inputPath))
                {
                    using var es = entry.Open();
                    es.CopyTo(fs);
                }

                var outputPath = Path.Combine(workDir, outBase);
                if (CompressMedia(inputPath, outputPath, ct))
                {
                    var bytes = File.ReadAllBytes(outputPath);
                    if (bytes.Length < entry.Length) // 压缩后确实变小才替换
                    {
                        renameMap[baseName] = outBase;
                        compressedBytes[outBase] = bytes;
                        _logger.LogInformation(
                            "[PptxPreprocess] {Media}: {Before} -> {After} bytes",
                            baseName, entry.Length, bytes.Length);
                    }
                }
            }

            if (renameMap.Count == 0)
            {
                _logger.LogInformation(
                    "[PptxPreprocess] 无可压缩媒体: {Source}", Path.GetFileName(sourcePath));
                return false;
            }

            WriteLightPptx(sourceZip, outPath, renameMap, compressedBytes, ct);
            _logger.LogInformation(
                "[PptxPreprocess] 完成: {Source} ({Compressed} 个媒体被压缩, 原大小 {Size}MB)",
                Path.GetFileName(sourcePath), renameMap.Count,
                new FileInfo(sourcePath).Length / 1048576);
            return true;
        }
        catch (InvalidDataException idex)
        {
            _logger.LogWarning(idex, "[PptxPreprocess] 源文件 Zip 结构损坏，跳过预压缩: {Source}", sourcePath);
            return false;
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// ffmpeg 压缩单个媒体。GIF 取中间帧转 JPEG（PDF 预览是静态的）；
    /// 静态大图缩放到目标宽度转 JPEG。返回是否成功。
    /// </summary>
    private bool CompressMedia(string inputPath, string outputPath, CancellationToken ct)
    {
        try
        {
            string filter;
            var maxDim = _options.PptxMaxImageDimension;
            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            if (ext == ".gif")
            {
                // GIF：取中间帧。先探测总帧数
                var frames = ProbeGifFrames(inputPath, ct);
                var mid = Math.Max(0, frames / 2);
                var scale = maxDim > 0 ? $"scale='min({maxDim},iw)':-2" : "null";
                filter = $"select=eq(n\\,{mid}),{scale}";
            }
            else
            {
                // 静态图：整体缩放
                filter = maxDim > 0 ? $"scale='min({maxDim},iw)':-2" : "null";
            }

            var args = new List<string>
            {
                "-y",
                "-i", inputPath,
                "-vf", filter,
                "-frames:v", "1",
                "-q:v", _options.PptxJpegQuality.ToString(),
                outputPath
            };

            return RunFfmpeg(args, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PptxPreprocess] ffmpeg 压缩失败: {Input}", inputPath);
            return false;
        }
    }

    private int ProbeGifFrames(string path, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in new[]
            {
                "-v", "error", "-count_frames", "-select_streams", "v:0",
                "-show_entries", "stream=nb_read_frames", "-of", "csv=p=0", path
            })
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc == null) return 1;
            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(15000);
            if (int.TryParse(stdout.Trim().Split('\n')[0], out var n) && n > 0)
                return n;
        }
        catch { /* ignore */ }
        return 1;
    }

    private bool RunFfmpeg(List<string> args, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.FfmpegTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var psi = new ProcessStartInfo
        {
            FileName = _options.FfmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            _logger.LogWarning("[PptxPreprocess] 无法启动 ffmpeg: {Path}", _options.FfmpegPath);
            return false;
        }

        var stderrTask = proc.StandardError.ReadToEndAsync();
        try
        {
            proc.WaitForExit((int)TimeSpan.FromSeconds(_options.FfmpegTimeoutSeconds).TotalMilliseconds);
            if (!proc.HasExited)
            {
                proc.Kill(true);
                return false;
            }
            if (proc.ExitCode != 0)
            {
                _logger.LogWarning("[PptxPreprocess] ffmpeg 退出码 {Code}: {Err}",
                    proc.ExitCode, stderrTask.Result[..Math.Min(500, stderrTask.Result.Length)]);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PptxPreprocess] ffmpeg 异常");
            return false;
        }
    }

    /// <summary>
    /// 组装 light PPTX：替换大媒体 + 重写引用它们的 rels/content-types/presentation。
    /// </summary>
    private void WriteLightPptx(
        ZipArchive sourceZip,
        string outPath,
        IReadOnlyDictionary<string, string> renameMap,
        IReadOnlyDictionary<string, byte[]> compressedBytes,
        CancellationToken ct)
    {
        var tmpPath = outPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];

        using (var outZip = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
        {
            foreach (var entry in sourceZip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var name = entry.FullName;

                // 媒体文件：被重命名的用新内容写出，否则原样复制
                if (name.StartsWith("ppt/media/", StringComparison.OrdinalIgnoreCase))
                {
                    var baseName = Path.GetFileName(name);
                    if (renameMap.TryGetValue(baseName, out var newBase) &&
                        compressedBytes.TryGetValue(newBase, out var bytes))
                    {
                        var newName = name.Replace(baseName, newBase);
                        var newEntry = outZip.CreateEntry(newName, CompressionLevel.Fastest);
                        using var s = newEntry.Open();
                        s.Write(bytes, 0, bytes.Length);
                        continue;
                    }
                }

                // .rels：先处理 presentation.xml.rels（剥离字体关系），再重写媒体引用
                if (name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                {
                    using var es = entry.Open();
                    using var reader = new StreamReader(es, Encoding.UTF8);
                    var xml = reader.ReadToEnd();

                    if (_options.StripEmbeddedFonts && name == "ppt/_rels/presentation.xml.rels")
                    {
                        xml = StripFontRelationships(xml);
                    }

                    var rewritten = RewriteRelsTargets(xml, renameMap);
                    var newEntry = outZip.CreateEntry(name, CompressionLevel.Optimal);
                    using var s = newEntry.Open();
                    var data = Encoding.UTF8.GetBytes(rewritten);
                    s.Write(data, 0, data.Length);
                    continue;
                }

                // presentation.xml：可选剥离嵌入字体
                if (name == "ppt/presentation.xml" && _options.StripEmbeddedFonts)
                {
                    using var es = entry.Open();
                    using var reader = new StreamReader(es, Encoding.UTF8);
                    var xml = reader.ReadToEnd();
                    var stripped = Regex.Replace(
                        xml, @"<p:embeddedFontLst>.*?</p:embeddedFontLst>", "",
                        RegexOptions.Singleline);
                    var newEntry = outZip.CreateEntry(name, CompressionLevel.Optimal);
                    using var s = newEntry.Open();
                    var data = Encoding.UTF8.GetBytes(stripped);
                    s.Write(data, 0, data.Length);
                    continue;
                }

                // 字体文件：剥离时跳过
                if (_options.StripEmbeddedFonts && name.StartsWith("ppt/fonts/", StringComparison.OrdinalIgnoreCase)
                    && name != "ppt/fonts/")
                {
                    continue;
                }

                // [Content_Types].xml：注入 jpg 默认声明（GIF/大图被转为 .jpg）
                if (name == "[Content_Types].xml")
                {
                    using var es = entry.Open();
                    using var reader = new StreamReader(es, Encoding.UTF8);
                    var xml = reader.ReadToEnd();
                    if (!xml.Contains("Extension=\"jpg\"") && !xml.Contains("Extension=\"jpeg\""))
                    {
                        xml = xml.Replace(
                            "</Types>",
                            "<Default Extension=\"jpg\" ContentType=\"image/jpeg\"/></Types>");
                    }
                    else if (!xml.Contains("Extension=\"jpg\""))
                    {
                        xml = xml.Replace(
                            "Extension=\"jpeg\"",
                            "Extension=\"jpg\" ContentType=\"image/jpeg\"/><Default Extension=\"jpeg\"");
                    }
                    var newEntry = outZip.CreateEntry(name, CompressionLevel.Optimal);
                    using var s = newEntry.Open();
                    var data = Encoding.UTF8.GetBytes(xml);
                    s.Write(data, 0, data.Length);
                    continue;
                }

                // 其他条目原样复制（保持字节保真，避免破坏结构）
                var ce = outZip.CreateEntry(name, CompressionLevel.Optimal);
                using (var es2 = entry.Open())
                using (var s2 = ce.Open())
                {
                    es2.CopyTo(s2);
                }
            }
        }

        File.Move(tmpPath, outPath, overwrite: true);
    }

    private static string RewriteRelsTargets(string relsXml, IReadOnlyDictionary<string, string> renameMap)
    {
        return Regex.Replace(relsXml, @"Target=""([^""]+)""", m =>
        {
            var target = m.Groups[1].Value;
            var baseName = target.Split('/').Last();
            if (renameMap.TryGetValue(baseName, out var newName))
                return $"Target=\"{target.Replace(baseName, newName)}\"";
            return m.Value;
        });
    }

    private static string StripFontRelationships(string relsXml)
    {
        return Regex.Replace(
            relsXml,
            @"<Relationship [^>]*?/relationships/font[^>]*?/>",
            "",
            RegexOptions.Singleline);
    }
}
