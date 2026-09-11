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
    private readonly IFfmpegRunner _runner;
    private readonly OfficeConversionOptions _options;
    private readonly ILogger<PptxImagePreprocessor> _logger;

    public PptxImagePreprocessor(
        IFileStorageService fileStorageService,
        IFfmpegRunner runner,
        IOptions<OfficeConversionOptions> options,
        ILogger<PptxImagePreprocessor> logger)
    {
        _fileStorageService = fileStorageService;
        _runner = runner;
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
    public async Task<string?> GetOrCreateLightAsync(string resourceId, string sourcePath, CancellationToken ct = default)
    {
        var lightPath = GetLightPptxPath(resourceId);
        var metaPath = GetLightMetaPath(resourceId);

        if (File.Exists(lightPath) && IsCacheValid(metaPath, sourcePath))
        {
            return lightPath;
        }

        // 无效缓存先清理，避免并发读到旧文件
        TryDelete(lightPath);
        TryDelete(metaPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lightPath)!);
            if (await PreprocessAsync(sourcePath, lightPath, ct))
            {
                SaveMeta(metaPath, sourcePath);
                return lightPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PptxPreprocess] 预压缩失败: {Source}", sourcePath);
            TryDelete(lightPath);
        }

        return null;
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
    private async Task<bool> PreprocessAsync(string sourcePath, string outPath, CancellationToken ct)
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
                if (await CompressMediaAsync(inputPath, outputPath, ct))
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
    private async Task<bool> CompressMediaAsync(string inputPath, string outputPath, CancellationToken ct)
    {
        try
        {
            var maxDim = _options.PptxMaxImageDimension;
            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            var args = new List<string> { "-y" };

            if (ext == ".gif")
            {
                // GIF：输入前用 -ss 定位到中间时刻抽一帧。
                // 原实现用 ffprobe -count_frames 全量解码数帧，对大 GIF 很贵；
                // 改为读时长取中点（只读容器索引，不解码）。
                var midSeconds = ProbeMediaMidSeconds(inputPath);
                if (midSeconds > 0)
                {
                    args.Add("-ss");
                    args.Add(midSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            var scale = maxDim > 0 ? $"scale='min({maxDim},iw)':-2" : "null";
            args.Add("-i");
            args.Add(inputPath);
            args.Add("-vf");
            args.Add(scale);
            args.Add("-frames:v");
            args.Add("1");
            args.Add("-q:v");
            args.Add(_options.PptxJpegQuality.ToString(System.Globalization.CultureInfo.InvariantCulture));
            args.Add(outputPath);

            var result = await _runner.RunFfmpegAsync(args, ct: ct);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PptxPreprocess] ffmpeg 压缩失败: {Input}", inputPath);
            return false;
        }
    }

    /// <summary>
    /// 用 ffprobe 读取媒体时长并返回中点秒数。失败返回 0（调用方退化为取首帧）。
    /// </summary>
    private double ProbeMediaMidSeconds(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in new[]
            {
                "-v", "error", "-show_entries", "format=duration",
                "-of", "default=noprint_wrappers=1:nokey=1", path
            })
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc == null) return 0;
            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            if (double.TryParse(
                    stdout.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var duration)
                && duration > 0)
            {
                return duration / 2.0;
            }
        }
        catch { /* ignore */ }
        return 0;
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
