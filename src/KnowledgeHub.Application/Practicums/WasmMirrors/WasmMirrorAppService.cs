using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Practicums.WasmMirrors;

/// <summary>
/// 扫描本地 WASM 镜像根目录并对外提供 sourceUrl → publicUrl 映射。
///
/// 设计要点：
/// - 缓存：60s 内存缓存 + 根目录 mtime 失效。无需持久化、无需定时刷新。
/// - 失败容忍：单个 mirror.json 解析失败只记录 warning 不影响其他 slug。
/// - 安全：拒绝 RootPath 含 <c>..</c> 跳出；scratch staging 目录不会进入公共映射。
/// </summary>
public class WasmMirrorAppService : ApplicationService, IWasmMirrorAppService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IOptions<WasmMirrorOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<WasmMirrorAppService> _logger;

    private readonly object _cacheLock = new();
    private DateTime _cacheExpiresAt = DateTime.MinValue;
    private DateTime _cacheScannedDirectoryMtime = DateTime.MinValue;
    private List<WasmMirrorInfoDto> _cacheItems = new();

    public WasmMirrorAppService(
        IOptions<WasmMirrorOptions> options,
        IHostEnvironment env,
        ILogger<WasmMirrorAppService> logger)
    {
        _options = options;
        _env = env;
        _logger = logger;
    }

    public Task<List<WasmMirrorMappingDto>> GetMappingAsync()
    {
        var items = GetCachedOrScan();
        var mappings = items
            .Where(x => string.Equals(x.Status, "ready", StringComparison.OrdinalIgnoreCase))
            .Select(x => new WasmMirrorMappingDto
            {
                SourceUrl = x.SourceUrl,
                Slug = x.Slug,
                PublicUrl = x.PublicUrl,
                Status = x.Status,
            })
            .Where(x => !string.IsNullOrEmpty(x.SourceUrl))
            .OrderBy(x => x.SourceUrl, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(mappings);
    }

    public Task<List<WasmMirrorInfoDto>> GetAllAsync()
    {
        var items = GetCachedOrScan();
        return Task.FromResult(items.OrderBy(x => x.Slug, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private List<WasmMirrorInfoDto> GetCachedOrScan()
    {
        var root = ResolveRootPath();
        var enabled = _options.Value.Enabled;
        var requireManifest = _options.Value.RequireManifest;
        var publicBase = NormalizePublicBase(_options.Value.PublicBasePath);
        var selfUrl = (_env.ContentRootPath ?? string.Empty).TrimEnd('/');

        if (!enabled || string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            if (!enabled)
            {
                _logger.LogDebug("WasmMirror disabled by configuration; returning empty mapping.");
            }
            else if (!Directory.Exists(root))
            {
                _logger.LogDebug("WasmMirror root not found: {Path}; returning empty mapping.", root);
            }
            return new List<WasmMirrorInfoDto>();
        }

        var currentMtime = SafeGetDirectoryLastWriteTime(root);
        lock (_cacheLock)
        {
            if (_cacheItems.Count > 0
                && DateTime.UtcNow < _cacheExpiresAt
                && currentMtime == _cacheScannedDirectoryMtime)
            {
                return _cacheItems;
            }

            var items = ScanInternal(root, requireManifest, publicBase, selfUrl);
            _cacheItems = items;
            _cacheScannedDirectoryMtime = currentMtime;
            _cacheExpiresAt = DateTime.UtcNow + CacheTtl;
            return items;
        }
    }

    private List<WasmMirrorInfoDto> ScanInternal(
        string root,
        bool requireManifest,
        string publicBase,
        string selfUrl)
    {
        var result = new List<WasmMirrorInfoDto>();
        IEnumerable<string> slugDirs;
        try
        {
            slugDirs = Directory.EnumerateDirectories(root);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate WasmMirror root {Root}", root);
            return result;
        }

        foreach (var dir in slugDirs)
        {
            var dirName = Path.GetFileName(dir);
            // 跳过 staging / 草稿目录，避免污染公共映射
            if (dirName.StartsWith(".", StringComparison.Ordinal) || dirName.StartsWith(".staging", StringComparison.Ordinal))
            {
                continue;
            }

            var manifestPath = Path.Combine(dir, "mirror.json");
            WasmMirrorManifest? manifest = null;
            try
            {
                if (File.Exists(manifestPath))
                {
                    var json = File.ReadAllText(manifestPath);
                    manifest = JsonSerializer.Deserialize<WasmMirrorManifest>(json, JsonOptions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse mirror.json at {Path}; skipping.", manifestPath);
            }

            // 未要求 manifest 或 manifest 缺失：尝试按 entry 推断
            var entryPath = manifest?.EntryPath;
            var statusFromManifest = manifest?.Status?.Trim().ToLowerInvariant();
            var status = statusFromManifest ?? "missing";

            // 没 manifest 且 RequireManifest=true → 整目录当作 missing
            if (manifest == null && requireManifest)
            {
                status = "missing";
                entryPath ??= "index.html";
            }
            else if (manifest != null)
            {
                entryPath ??= "index.html";
            }

            var entryFullPath = string.IsNullOrEmpty(entryPath)
                ? null
                : Path.Combine(dir, entryPath);
            if (status == "ready" && (entryFullPath == null || !File.Exists(entryFullPath)))
            {
                _logger.LogWarning(
                    "WasmMirror {Slug} declared ready but entry {Entry} missing.",
                    dirName, entryFullPath ?? "(null)");
                status = "invalid";
            }

            long bytes = 0;
            int fileCount = 0;
            DateTime? lastModified = null;
            List<string> files = new();
            try
            {
                if (Directory.Exists(dir))
                {
                    var rootFull = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
                    foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        if (f.StartsWith(rootFull, StringComparison.Ordinal))
                        {
                            var rel = f.Substring(rootFull.Length);
                            if (rel.Equals("mirror.json", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            files.Add(rel);
                        }

                        var info = new FileInfo(f);
                        if (info.Name.Equals("mirror.json", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        bytes += info.Length;
                        fileCount++;
                        var lm = info.LastWriteTimeUtc;
                        if (lastModified == null || lm > lastModified) lastModified = lm;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate files under {Dir}", dir);
            }

            var sourceUrl = NormalizeSourceUrl(manifest?.SourceUrl ?? string.Empty);
            var publicUrl = string.IsNullOrEmpty(sourceUrl)
                ? string.Empty
                : BuildPublicUrl(selfUrl, publicBase, dirName, entryPath ?? "index.html");

            result.Add(new WasmMirrorInfoDto
            {
                SourceUrl = sourceUrl,
                Slug = dirName,
                PublicUrl = publicUrl,
                Status = status,
                EntryPath = entryPath ?? "index.html",
                FileCount = fileCount,
                TotalBytes = bytes,
                LastModifiedTime = lastModified,
                MirroredAt = manifest?.MirroredAt,
                BuildSha = manifest?.BuildSha,
                CoopCoepRequired = manifest?.CoopCoepRequired ?? true,
                Files = files,
                Title = !string.IsNullOrWhiteSpace(manifest?.Title)
                    ? manifest!.Title!.Trim()
                    : BeautifySlug(dirName),
                Cover = string.IsNullOrWhiteSpace(manifest?.Cover) ? null : manifest!.Cover!.Trim(),
                Description = string.IsNullOrWhiteSpace(manifest?.Description) ? null : manifest!.Description!.Trim(),
            });
        }

        return result;
    }

    private string ResolveRootPath()
    {
        var raw = _options.Value.RootPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var resolved = Path.IsPathRooted(raw)
            ? raw
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath ?? AppContext.BaseDirectory, raw));

        // 安全检查：拒绝包含 ".." 的路径段
        var segments = resolved.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(s => s == ".."))
        {
            _logger.LogWarning("WasmMirror root path contains '..' segment: {Path}; disabling.", resolved);
            return string.Empty;
        }
        return resolved;
    }

    private static string NormalizePublicBase(string publicBase)
    {
        if (string.IsNullOrWhiteSpace(publicBase))
        {
            return "/wasm";
        }
        var pb = publicBase.Trim();
        if (!pb.StartsWith("/")) pb = "/" + pb;
        return pb.TrimEnd('/');
    }

    private static string NormalizeSourceUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var u))
        {
            // 不是绝对 URL：保留原值（去掉末尾斜杠）
            return trimmed.TrimEnd('/');
        }
        var builder = new UriBuilder(u)
        {
            Query = string.Empty,
            Fragment = string.Empty,
        };
        var path = builder.Path.TrimEnd('/');
        var sb = new System.Text.StringBuilder();
        sb.Append(builder.Scheme).Append("://").Append(builder.Host);
        var port = builder.Port;
        var isDefault = (string.Equals(builder.Scheme, "http", StringComparison.OrdinalIgnoreCase) && port == 80)
                        || (string.Equals(builder.Scheme, "https", StringComparison.OrdinalIgnoreCase) && port == 443);
        if (!isDefault)
        {
            sb.Append(':').Append(port);
        }
        sb.Append(path);
        return sb.ToString();
    }

    private static string BuildPublicUrl(string selfUrl, string publicBase, string slug, string entryPath)
    {
        var entry = (entryPath ?? "index.html").TrimStart('/');
        var basePart = publicBase.TrimEnd('/');
        var url = $"{basePart}/{Uri.EscapeDataString(slug)}/{entry}";
        // escapeDataString 把小写 + 短横线保留 OK；如果未来 slug 含奇怪字符再调整
        url = url.Replace("%2F", "/");
        return url;
    }

    private static DateTime SafeGetDirectoryLastWriteTime(string path)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// mirror.json schema。字段缺失按默认处理。
    /// </summary>
    private class WasmMirrorManifest
    {
        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }

        [JsonPropertyName("entryPath")]
        public string? EntryPath { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("mirroredAt")]
        public DateTime? MirroredAt { get; set; }

        [JsonPropertyName("buildSha")]
        public string? BuildSha { get; set; }

        [JsonPropertyName("coopCoepRequired")]
        public bool? CoopCoepRequired { get; set; }

        [JsonPropertyName("files")]
        public List<string>? Files { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("cover")]
        public string? Cover { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// 当 manifest 没有 title 时，把 slug 美化为展示用标题：
    ///   <c>anatomy-mice</c> → <c>Anatomy Mice</c>。
    /// </summary>
    private static string BeautifySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return string.Empty;
        var spaced = slug.Replace('-', ' ').Replace('_', ' ').Trim();
        if (spaced.Length == 0) return slug;
        // 首字母大写（每个单词）
        var parts = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0) continue;
            parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1);
        }
        return string.Join(' ', parts);
    }
}
