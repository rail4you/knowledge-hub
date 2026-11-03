using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aliyun.OSS;
using KnowledgeHub.Resources.Oss;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.DoubleHigh;

/// <summary>
/// 评估佐证文件存储：**优先 OSS**（多实例部署共享），未配置 OSS 时回退本地磁盘。
///
/// StoredPath 用前缀区分后端，历史数据无需迁移：
/// - <c>oss:{objectKey}</c> —— 阿里云 OSS（复用管理页维护的租户级 AK/SK/Bucket 配置）
/// - <c>local:{relativePath}</c> —— 本地 App_Data/assessment-evidence（开发环境 / 未配置 OSS）
/// </summary>
public class DoubleHighEvidenceFileStore : ITransientDependency
{
    public const string OssPrefix = "oss:";
    public const string LocalPrefix = "local:";

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IOssCredentialProvider _ossCredentials;
    private readonly ILogger<DoubleHighEvidenceFileStore> _logger;

    public DoubleHighEvidenceFileStore(
        IHostEnvironment environment,
        IConfiguration configuration,
        IOssCredentialProvider ossCredentials,
        ILogger<DoubleHighEvidenceFileStore> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _ossCredentials = ossCredentials;
        _logger = logger;
    }

    public string RootPath
    {
        get
        {
            var configured = _configuration["DoubleHigh:EvidenceRoot"];
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(_environment.ContentRootPath, "App_Data", "assessment-evidence")
                : Path.GetFullPath(configured);
        }
    }

    /// <summary>
    /// 保存佐证文件。返回 (存储路径, 公开访问 URL)。
    /// </summary>
    public async Task<(string StoredPath, string Url)> SaveAsync(
        Guid projectId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (content == null || content.Length == 0)
        {
            throw new UserFriendlyException("佐证文件内容为空");
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        var safeName = $"{Guid.NewGuid():N}{extension}";

        try
        {
            var options = await _ossCredentials.GetOptionsAsync();
            if (options.IsConfigured)
            {
                var objectKey = $"{options.NormalizedUploadPath}/assessment-evidence/{projectId:N}/{safeName}";
                var client = _ossCredentials.CreateClient(options);
                var metadata = new ObjectMetadata
                {
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    ContentLength = content.Length,
                };

                using var stream = new MemoryStream(content);
                await Task.Run(() => client.PutObject(options.BucketName, objectKey, stream, metadata), cancellationToken);

                _logger.LogInformation("评估佐证已上传 OSS：{ObjectKey}（{Size} bytes）", objectKey, content.Length);
                return (OssPrefix + objectKey, options.BuildObjectUrl(objectKey));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "评估佐证上传 OSS 失败，回退本地存储");
        }

        var relative = Path.Combine(projectId.ToString("N"), safeName);
        var fullPath = EnsureUnderRoot(Path.Combine(RootPath, relative));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        // 本地存储没有公开 URL，前端通过下载接口按 evidenceId 取文件。
        return (LocalPrefix + relative.Replace('\\', '/'), string.Empty);
    }

    /// <summary>读取佐证文件内容（导出资料包时把原始文件打进 zip）。</summary>
    public async Task<byte[]> ReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        if (IsOss(storedPath))
        {
            var objectKey = storedPath!.Substring(OssPrefix.Length);
            var options = await _ossCredentials.GetOptionsAsync();
            if (!options.IsConfigured)
            {
                throw new UserFriendlyException("该佐证文件归档在 OSS，但当前未配置 OSS 访问凭证，请联系管理员");
            }

            var client = _ossCredentials.CreateClient(options);
            using var ossObject = client.GetObject(options.BucketName, objectKey);
            using var memory = new MemoryStream();
            await ossObject.Content.CopyToAsync(memory, cancellationToken);
            return memory.ToArray();
        }

        var relativePath = StripLocalPrefix(storedPath);
        var fullPath = EnsureUnderRoot(Path.Combine(RootPath, relativePath));
        if (!File.Exists(fullPath))
        {
            throw new UserFriendlyException("佐证文件不存在，可能已被清理，请重新上传");
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    private static bool IsOss(string? storedPath) =>
        !string.IsNullOrWhiteSpace(storedPath) &&
        storedPath.StartsWith(OssPrefix, StringComparison.OrdinalIgnoreCase);

    private static string StripLocalPrefix(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return string.Empty;
        }

        return storedPath.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase)
            ? storedPath[LocalPrefix.Length..]
            : storedPath;
    }

    private string EnsureUnderRoot(string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(RootPath) + Path.DirectorySeparatorChar;
        var normalized = Path.GetFullPath(fullPath);
        if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("非法的佐证文件路径");
        }

        return normalized;
    }
}
