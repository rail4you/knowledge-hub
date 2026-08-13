using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 使用 Gotenberg（HTTP 服务，内部基于 LibreOffice headless）将 Office 文档转为 PDF。
/// 转换结果缓存到 FileStorage/converted/{resourceId}.pdf，避免重复转换。
/// 队列调度由 Hangfire 负责；并发限流由 ConversionConcurrencyManager 按服务分组控制。
/// </summary>
public class GotenbergConversionService : IOfficeConversionService, ISingletonDependency
{
    public const string GotenbergHttpClientName = "Gotenberg";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileStorageService _fileStorageService;
    private readonly OfficeConversionOptions _options;
    private readonly ConversionConcurrencyManager _concurrencyManager;
    private readonly ILogger<GotenbergConversionService> _logger;

    /// <summary>
    /// 同一 resourceId 的并发请求复用同一次转换。
    /// key: resourceId, value: Lazy<Task<string>>
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new();

    public GotenbergConversionService(
        IHttpClientFactory httpClientFactory,
        IFileStorageService fileStorageService,
        IOptions<OfficeConversionOptions> options,
        ConversionConcurrencyManager concurrencyManager,
        ILogger<GotenbergConversionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fileStorageService = fileStorageService;
        _options = options.Value;
        _concurrencyManager = concurrencyManager;
        _logger = logger;
    }

    /// <summary>
    /// 该资源是否已有转换任务在排队/执行中（Hangfire job 已 enqueue 或正在转换）。
    /// </summary>
    public bool IsInFlight(string resourceId)
    {
        return _inflight.ContainsKey(resourceId);
    }

    public bool HasCachedPdf(string resourceId)
    {
        var path = GetCachedPdfPath(resourceId);
        return File.Exists(path);
    }

    /// <summary>
    /// 完整 PDF 缓存是否对应当前源文件（存在且 meta 有效）。
    /// 用于 /preview-pdf-info 判断是否已就绪，避免依赖逐页拆分产物。
    /// 截断 PPTX 的缓存 meta 记录的是"修复后文件"的有效信息，需与 ConvertToPdfAsync 一致。
    /// </summary>
    public bool HasValidCachedPdf(string resourceId, string sourcePath)
    {
        if (!File.Exists(sourcePath)) return false;

        var effectiveSource = sourcePath;
        if (IsPptxFile(sourcePath) && !IsValidZip(sourcePath))
        {
            var repairedPath = GetRepairedPptxPath(resourceId);
            if (!File.Exists(repairedPath)) return false;
            effectiveSource = repairedPath;
        }

        var cachedPath = GetCachedPdfPath(resourceId);
        if (!File.Exists(cachedPath)) return false;
        return IsCacheValid(GetCacheMetaPath(resourceId), effectiveSource);
    }

    public void InvalidateCache(string resourceId)
    {
        var path = GetCachedPdfPath(resourceId);
        var metaPath = GetCacheMetaPath(resourceId);
        var repairedPath = GetRepairedPptxPath(resourceId);
        var repairMetaPath = GetRepairMetaPath(resourceId);
        var pagesDir = Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            resourceId);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("[OfficeConversion] 缓存已清除: {Path}", path);
            }
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
            if (File.Exists(repairedPath))
            {
                File.Delete(repairedPath);
                _logger.LogInformation("[OfficeConversion] 修复缓存已清除: {Path}", repairedPath);
            }
            if (File.Exists(repairMetaPath))
            {
                File.Delete(repairMetaPath);
            }
            if (Directory.Exists(pagesDir))
            {
                Directory.Delete(pagesDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OfficeConversion] 清除缓存失败: {Path}", path);
        }
    }

    public async Task<string> ConvertToPdfAsync(
        string resourceId,
        string sourcePath,
        string serviceName = "preview",
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new OfficeConversionException($"源文件不存在: {sourcePath}");

        // 截断/损坏的 PPTX（缺少 ZIP 中央目录）LibreOffice 无法加载。
        // 先重建 ZIP 中央目录修复，使转换可以正常进行（保持原始版式）。
        var effectiveSource = sourcePath;
        if (IsPptxFile(sourcePath) && !IsValidZip(sourcePath))
        {
            effectiveSource = await GetOrCreateRepairedAsync(resourceId, sourcePath, cancellationToken);
        }

        // 1. 检查缓存是否有效（对比源文件修改时间）
        var cachedPath = GetCachedPdfPath(resourceId);
        if (File.Exists(cachedPath) && IsCacheValid(GetCacheMetaPath(resourceId), effectiveSource))
        {
            _logger.LogDebug("[OfficeConversion] 缓存命中: {ResourceId} -> {Path}", resourceId, cachedPath);
            return cachedPath;
        }

        if (File.Exists(cachedPath))
        {
            _logger.LogInformation("[OfficeConversion] 缓存已过期（源文件已变化），重新转换: {ResourceId}", resourceId);
            InvalidateCache(resourceId);
        }

        // 2. 同一资源的并发请求复用同一次 Task（避免双转换）。
        // 注意：共享任务使用 CancellationToken.None，而非第一个调用方的 token——
        // 否则某个浏览器标签页断开连接会连带取消所有等待该资源的预览。
        // 转换自身仍有内部超时（ConversionTimeoutSeconds）兜底。
        // 降采样阈值基于原始源文件大小判断（修复/预处理可能缩小文件导致漏判）。
        var useDownsampling = _options.MaxImageResolutionDpi > 0 &&
                              new FileInfo(sourcePath).Length > _options.ReduceImageResolutionThresholdBytes;
        var lazy = _inflight.GetOrAdd(
            resourceId,
            id => new Lazy<Task<string>>(() => DoConvertAsync(id, effectiveSource, serviceName, useDownsampling, CancellationToken.None)));

        try
        {
            return await lazy.Value;
        }
        finally
        {
            // 任务完成后从 inflight 字典移除（不影响已完成缓存的命中）
            _inflight.TryRemove(resourceId, out _);
        }
    }

    private async Task<string> DoConvertAsync(
        string resourceId,
        string sourcePath,
        string serviceName,
        bool useDownsampling,
        CancellationToken cancellationToken)
    {
        // 并发闸门：按服务分组限制同时转换数（默认 preview=1，reprocess=1，可 API 调）。
        // 队列（排队）由 Hangfire 承担，这里只做严格并发控制。
        using var gate = await _concurrencyManager.GetGate(serviceName).AcquireAsync(cancellationToken);
        return await ConvertViaGotenbergAsync(resourceId, sourcePath, useDownsampling, cancellationToken);
    }

    private async Task<string> ConvertViaGotenbergAsync(
        string resourceId,
        string sourcePath,
        bool useDownsampling,
        CancellationToken cancellationToken)
    {
        // 准备临时工作目录
        var workDir = Path.Combine(Path.GetTempPath(), $"gotenberg-{resourceId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        var sourceExt = Path.GetExtension(sourcePath)?.ToLowerInvariant();
        var workSourcePath = Path.Combine(workDir, $"source{sourceExt}");

        var targetPdfPath = GetCachedPdfPath(resourceId);

        try
        {
            // PPTX: 预处理，去掉隐藏幻灯片标记，确保 LibreOffice 导出全部幻灯片
            var actualSourcePath = sourceExt == ".pptx" || sourceExt == ".ppt"
                ? PreparePptxWithAllSlidesVisible(sourcePath, workDir)
                : sourcePath;

            File.Copy(actualSourcePath, workSourcePath, overwrite: true);

            var sw = Stopwatch.StartNew();

            // 通过 Gotenberg 的 LibreOffice 端点转换。
            // 字段名 "files"，扩展名决定解析格式，必须保留源文件扩展名。
            var http = _httpClientFactory.CreateClient(GotenbergHttpClientName);
            using var form = new MultipartFormDataContent();
            await using (var fs = new FileStream(workSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fileContent = new StreamContent(fs);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(sourceExt));
                form.Add(fileContent, "files", Path.GetFileName(workSourcePath));

                // 大文件降采样：超过阈值的文档在转换时把嵌入图片降到 maxImageResolution DPI，
                // 显著减小输出 PDF 体积与 LibreOffice 处理内存/CPU，降低服务器压力。
                // 仅对 office 文档生效；在线预览清晰度在 150 DPI 下足够。
                if (useDownsampling)
                {
                    form.Add(new StringContent("true"), "reduceImageResolution");
                    form.Add(new StringContent(_options.MaxImageResolutionDpi.ToString()), "maxImageResolution");
                    if (_options.JpegQuality > 0)
                    {
                        form.Add(new StringContent(_options.JpegQuality.ToString()), "quality");
                    }
                    _logger.LogInformation(
                        "[OfficeConversion] 大文件启用图片降采样: {ResourceId}, 源大小 {Size} -> {Dpi}DPI",
                        resourceId, fs.Length, _options.MaxImageResolutionDpi);
                }

                _logger.LogInformation(
                    "[OfficeConversion] 开始转换: {ResourceId}, source: {Source}, gotenberg: {BaseUrl}",
                    resourceId, workSourcePath, _options.BaseUrl);

                using var timeoutCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(_options.ConversionTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                try
                {
                    using var response = await http.PostAsync(
                        "/forms/libreoffice/convert", form, linkedCts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(linkedCts.Token);
                        _logger.LogError(
                            "[OfficeConversion] Gotenberg 转换失败: {Status} - {Error}",
                            (int)response.StatusCode, Truncate(error, 500));
                        throw new OfficeConversionException(
                            $"Gotenberg 文档转换失败（{(int)response.StatusCode}）: {Truncate(error, 500)}");
                    }

                    // 流式下载 PDF 直接写盘，避免整份 PDF 载入内存（大文件可省几十~几百 MB）。
                    // 原子写入：先写临时文件再 rename，避免半截文件被缓存命中逻辑读到。
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPdfPath)!);
                    var tmpPath = targetPdfPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
                    long pdfLength;
                    await using (var fsOut = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await response.Content.CopyToAsync(fsOut, linkedCts.Token);
                        pdfLength = fsOut.Length;
                    }

                    sw.Stop();

                    if (pdfLength == 0)
                    {
                        try { File.Delete(tmpPath); } catch { /* ignore */ }
                        throw new OfficeConversionException("Gotenberg 转换完成但未生成 PDF 内容");
                    }

                    File.Move(tmpPath, targetPdfPath, overwrite: true);
                    SaveCacheMeta(GetCacheMetaPath(resourceId), sourcePath);

                    _logger.LogInformation(
                        "[OfficeConversion] 转换成功: {ResourceId}, 耗时 {Elapsed}ms, 大小 {Size}",
                        resourceId, sw.ElapsedMilliseconds, pdfLength);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;
                    throw new OfficeConversionException(
                        $"Office 转换超时（{_options.ConversionTimeoutSeconds}s），文件可能过大或包含复杂内容");
                }
            }

            return targetPdfPath;
        }
        finally
        {
            // 清理临时工作目录
            try
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, recursive: true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// PPTX 预处理：去掉所有隐藏幻灯片标记 (show="0")，
    /// 确保 LibreOffice 导出全部幻灯片。返回处理后的文件路径。
    /// </summary>
    private string PreparePptxWithAllSlidesVisible(string sourcePath, string workDir)
    {
        var preppedPath = Path.Combine(workDir, "prepped.pptx");
        try
        {
            using var sourceZip = ZipFile.OpenRead(sourcePath);
            using var targetZip = ZipFile.Open(preppedPath, ZipArchiveMode.Create);

            var hasHiddenSlides = false;

            foreach (var entry in sourceZip.Entries)
            {
                if (entry.FullName.StartsWith("ppt/slides/slide") &&
                    entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    var doc = XDocument.Load(stream);
                    XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
                    var sld = doc.Root;
                    if (sld != null)
                    {
                        var showAttr = sld.Attribute("show");
                        if (showAttr != null && showAttr.Value == "0")
                        {
                            showAttr.Remove();
                            hasHiddenSlides = true;
                        }
                    }

                    var newEntry = targetZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var newStream = newEntry.Open();
                    doc.Save(newStream);
                }
                else
                {
                    // 直接复制其他条目
                    var newEntry = targetZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var sourceStream = entry.Open();
                    using var targetStream = newEntry.Open();
                    sourceStream.CopyTo(targetStream);
                }
            }

            if (hasHiddenSlides)
            {
                _logger.LogInformation(
                    "[OfficeConversion] 已移除 PPTX 隐藏幻灯片标记: {Source} -> {Prepped}",
                    Path.GetFileName(sourcePath), preppedPath);
                return preppedPath;
            }

            // 没有隐藏幻灯片，删除预处理文件，使用原始文件
            try { File.Delete(preppedPath); } catch { /* ignore */ }
            return sourcePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OfficeConversion] PPTX 预处理失败，使用原始文件");
            try { if (File.Exists(preppedPath)) File.Delete(preppedPath); } catch { /* ignore */ }
            return sourcePath;
        }
    }

    private string GetCachedPdfPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.pdf");
    }

    private string GetRepairMetaPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.repaired.meta");
    }

    /// <summary>
    /// 判断是否为 .pptx（仅 PPTX 是 ZIP 容器，可被本地文件头扫描修复）。
    /// 旧版 .ppt 为 OLE/CFB 二进制，走 Gotenberg 原生转换，不做 ZIP 修复。
    /// </summary>
    private static bool IsPptxFile(string path) =>
        string.Equals(Path.GetExtension(path), ".pptx", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断文件是否为可读取的标准 ZIP（存在有效中央目录）。
    /// 截断/损坏的 PPTX 缺少中央目录，ZipFile.OpenRead 会抛异常。
    /// </summary>
    private static bool IsValidZip(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 截断/损坏 PPTX（缺少 ZIP 中央目录，LibreOffice 无法加载）的修复入口。
    /// 修复结果缓存在 converted/{resourceId}.repaired.pptx，源文件变化时自动失效。
    /// </summary>
    private async Task<string> GetOrCreateRepairedAsync(
        string resourceId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var repairedPath = GetRepairedPptxPath(resourceId);
        var repairMetaPath = GetRepairMetaPath(resourceId);

        if (File.Exists(repairedPath) && IsCacheValid(repairMetaPath, sourcePath))
        {
            _logger.LogDebug("[OfficeConversion] 修复缓存命中: {ResourceId} -> {Path}", resourceId, repairedPath);
            return repairedPath;
        }

        var tmpPath = repairedPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        Directory.CreateDirectory(Path.GetDirectoryName(repairedPath)!);

        try
        {
            await Task.Run(() => RepairPptx(sourcePath, tmpPath), cancellationToken);

            File.Move(tmpPath, repairedPath, overwrite: true);
            SaveCacheMeta(repairMetaPath, sourcePath);

            _logger.LogInformation(
                "[OfficeConversion] 截断 PPTX 已修复: {ResourceId} -> {Path}",
                resourceId, repairedPath);
            return repairedPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* ignore */ }
            throw new OfficeConversionException($"PPTX 文件损坏且无法修复: {ex.Message}", ex);
        }
    }

    private sealed record RepairEntry(
        long Offset, string Name, ushort Method,
        int DataStart, int DataSize, bool Complete);

    /// <summary>
    /// 扫描本地文件头重建截断 PPTX 的 ZIP 结构：
    /// - flags=0 时本地头的压缩大小是权威的（本类损坏文件均无 data descriptor）
    /// - 带 data descriptor 的条目通过查找下一条 PK 签名定位
    /// - 数据越过文件末尾的条目（截断尾部）标记为不完整
    /// </summary>
    private static List<RepairEntry> ScanRepairEntries(byte[] bytes)
    {
        var entries = new List<RepairEntry>();
        var length = bytes.Length;
        var pos = 0;

        while (pos < length - 4)
        {
            if (bytes[pos] != 0x50 || bytes[pos + 1] != 0x4B ||
                bytes[pos + 2] != 0x03 || bytes[pos + 3] != 0x04)
            {
                pos++;
                continue;
            }

            if (pos + 30 > length) break;

            var method = (ushort)(bytes[pos + 8] | (bytes[pos + 9] << 8));
            var flags = (ushort)(bytes[pos + 6] | (bytes[pos + 7] << 8));
            var fileNameLen = bytes[pos + 26] | (bytes[pos + 27] << 8);
            var extraLen = bytes[pos + 28] | (bytes[pos + 29] << 8);
            var headerSize = 30 + fileNameLen + extraLen;

            if (pos + headerSize > length) break;

            string name;
            try
            {
                name = Encoding.UTF8.GetString(bytes, pos + 30, fileNameLen);
            }
            catch
            {
                pos++;
                continue;
            }

            var dataStart = pos + headerSize;
            var dataSize = bytes[pos + 18] | (bytes[pos + 19] << 8) |
                           (bytes[pos + 20] << 16) | (bytes[pos + 21] << 24);

            // 使用 data descriptor 的条目（flags bit3）：本地头中压缩大小为 0，
            // 需扫描下一条签名定位真实边界
            if ((flags & 8) != 0)
            {
                var next = FindNextPkSignature(bytes, length, dataStart);
                dataSize = next >= 0 ? next - dataStart : length - dataStart;
            }

            var complete = dataStart + dataSize <= length;
            entries.Add(new RepairEntry(pos, name, method, dataStart, dataSize, complete));

            pos = complete ? dataStart + dataSize : length;
        }

        return entries;
    }

    private static int FindNextPkSignature(byte[] buffer, int length, int start)
    {
        for (var i = start; i < length - 4; i++)
        {
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B &&
                buffer[i + 2] == 0x03 && buffer[i + 3] == 0x04)
                return i;
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B &&
                buffer[i + 2] == 0x01 && buffer[i + 3] == 0x02)
                return i; // 也停在中央目录签名前
        }
        return -1;
    }

    /// <summary>
    /// 重建 ZIP：校验每个条目解压，损坏的媒体替换为透明占位 PNG（保留引用关系，
    /// LibreOffice 渲染空白图），截断的尾部条目丢弃，写出有效中央目录。
    /// 有效条目按字节保真保留原始压缩数据。
    /// </summary>
    private void RepairPptx(string sourcePath, string outPath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        var entries = ScanRepairEntries(bytes);

        var centralEntries = new List<(byte[] Name, ushort Method, uint Crc, int CompressedSize, int UncompressedSize, long Offset)>();
        var dropped = 0;
        var placeholder = 0;

        using var fs = new FileStream(outPath, FileMode.Create);
        using var w = new BinaryWriter(fs);
        long offset = 0;

        foreach (var e in entries)
        {
            var nameBytes = Encoding.UTF8.GetBytes(e.Name);

            if (!e.Complete)
            {
                dropped++;
                continue;
            }

            byte[] data;
            ushort method;
            uint crc;
            var uncompressedSize = 0;

            if (e.Method == 0)
            {
                data = bytes.AsSpan(e.DataStart, e.DataSize).ToArray();
                method = 0;
                crc = Crc32(data);
                uncompressedSize = data.Length;
            }
            else if (e.Method == 8)
            {
                try
                {
                    var raw = Inflate(bytes, e.DataStart, e.DataSize);
                    data = bytes.AsSpan(e.DataStart, e.DataSize).ToArray(); // 字节保真保留压缩数据
                    method = 8;
                    crc = Crc32(raw);
                    uncompressedSize = raw.Length;
                }
                catch
                {
                    // 损坏的 deflate 数据：替换为透明占位 PNG
                    data = TransparentPng;
                    method = 0;
                    crc = Crc32(data);
                    uncompressedSize = data.Length;
                    placeholder++;
                }
            }
            else
            {
                // 不支持的压缩方法：占位
                data = TransparentPng;
                method = 0;
                crc = Crc32(data);
                uncompressedSize = data.Length;
                placeholder++;
            }

            WriteLocalHeader(w, method, crc, data.Length, uncompressedSize, nameBytes);
            w.Write(data);
            centralEntries.Add((nameBytes, method, crc, data.Length, uncompressedSize, offset));
            offset += 30 + nameBytes.Length + data.Length;
        }

        var cdStart = fs.Position;
        foreach (var ce in centralEntries)
        {
            w.Write(BuildCentralEntry(ce.Method, ce.Crc, ce.CompressedSize, ce.UncompressedSize, ce.Name, ce.Offset));
        }
        var cdSize = fs.Position - cdStart;

        // EOCD
        w.Write(0x06054b50);
        w.Write((ushort)0);   // disk number
        w.Write((ushort)0);   // disk with central dir
        w.Write((ushort)centralEntries.Count);
        w.Write((ushort)centralEntries.Count);
        w.Write((uint)cdSize);
        w.Write((uint)cdStart);
        w.Write((ushort)0);   // comment length
        w.Flush();

        if (dropped > 0 || placeholder > 0)
        {
            _logger.LogInformation(
                "[OfficeConversion] PPTX 修复完成: 总条目 {Total}, 占位替换 {Placeholder}, 丢弃 {Dropped}",
                entries.Count, placeholder, dropped);
        }
    }

    private static void WriteLocalHeader(
        BinaryWriter w, ushort method, uint crc,
        int compressedSize, int uncompressedSize, byte[] nameBytes)
    {
        w.Write(0x04034b50);
        w.Write((ushort)20);        // version needed
        w.Write((ushort)0);         // flags
        w.Write(method);
        w.Write((ushort)0);         // mod time
        w.Write((ushort)0);         // mod date
        w.Write(crc);
        w.Write(compressedSize);
        w.Write(uncompressedSize);
        w.Write((ushort)nameBytes.Length);
        w.Write((ushort)0);         // extra field length
        w.Write(nameBytes);
    }

    private static byte[] BuildCentralEntry(
        ushort method, uint crc,
        int compressedSize, int uncompressedSize, byte[] nameBytes, long offset)
    {
        using var ms = new MemoryStream(64);
        using var w = new BinaryWriter(ms);
        w.Write(0x02014b50);
        w.Write((ushort)20);        // version made by
        w.Write((ushort)20);        // version needed
        w.Write((ushort)0);         // flags
        w.Write(method);
        w.Write((ushort)0);         // mod time
        w.Write((ushort)0);         // mod date
        w.Write(crc);
        w.Write(compressedSize);
        w.Write(uncompressedSize);
        w.Write((ushort)nameBytes.Length);
        w.Write((ushort)0);         // extra field length
        w.Write((ushort)0);         // file comment length
        w.Write((ushort)0);         // disk number start
        w.Write((ushort)0);         // internal file attributes
        w.Write(0U);                // external file attributes
        w.Write((uint)offset);
        w.Write(nameBytes);
        w.Flush();
        return ms.ToArray();
    }

    private static byte[] Inflate(byte[] src, int offset, int count)
    {
        using var input = new MemoryStream(src, offset, count, writable: false);
        using var output = new MemoryStream(count * 2 + 64);
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        {
            deflate.CopyTo(output);
        }
        return output.ToArray();
    }

    /// <summary>1x1 透明 PNG，用于替换损坏的图片条目（保持 ZIP 内引用一致）。</summary>
    private static readonly byte[] TransparentPng = Convert.FromHexString(
        "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c489" +
        "0000000d49444154789c6360000002000100ffdf00bf0000000049454e44ae426082");

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320U ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFU;
        foreach (var b in data)
        {
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFU;
    }

    public string GetPagePdfPath(string resourceId, int pageNumber)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}",
            $"page{pageNumber}.pdf");
    }

    public string GetRepairedPptxPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.repaired.pptx");
    }

    private string GetCacheMetaPath(string resourceId)
    {
        return Path.Combine(
            _fileStorageService.RootPath,
            _options.CacheDirectory,
            $"{resourceId}.meta");
    }

    /// <summary>
    /// 通过源文件的 LastWriteTime + Length 对比来判断缓存是否有效。
    /// 源文件被替换（re-upload / new version）时自动失效。
    /// </summary>
    private bool IsCacheValid(string metaPath, string sourcePath)
    {
        if (!File.Exists(metaPath)) return false;

        try
        {
            var metaJson = File.ReadAllText(metaPath);
            var meta = System.Text.Json.JsonSerializer.Deserialize<CacheMeta>(metaJson);
            if (meta == null) return false;

            var sourceInfo = new FileInfo(sourcePath);
            return meta.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc
                && meta.Length == sourceInfo.Length;
        }
        catch
        {
            return false;
        }
    }

    private void SaveCacheMeta(string metaPath, string sourcePath)
    {
        try
        {
            var sourceInfo = new FileInfo(sourcePath);
            var meta = new CacheMeta
            {
                LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc,
                Length = sourceInfo.Length
            };
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
            File.WriteAllText(metaPath,
                System.Text.Json.JsonSerializer.Serialize(meta));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OfficeConversion] 保存缓存元数据失败: {Path}", metaPath);
        }
    }

    private class CacheMeta
    {
        public DateTime LastWriteTimeUtc { get; set; }
        public long Length { get; set; }
    }

    private static string GetMimeType(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";
}
