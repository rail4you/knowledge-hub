using System;
using System.Threading.Tasks;
using Aliyun.OSS;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace KnowledgeHub.Resources.Oss;

/// <summary>
/// 阿里云 OSS 连接参数（已合并数据库设置 + appsettings 回退）。
/// </summary>
public class OssStorageOptions
{
    public string Endpoint { get; set; } = "oss-cn-beijing.aliyuncs.com";

    public string AccessKeyId { get; set; } = "";

    public string AccessKeySecret { get; set; } = "";

    public string BucketName { get; set; } = "";

    /// <summary>对象键统一前缀（不带首尾斜杠）。</summary>
    public string UploadPath { get; set; } = "knowledgehub";

    /// <summary>自定义域名 / CDN 加速域名（含协议），为空时按 endpoint + bucket 推导。</summary>
    public string UrlPrefix { get; set; } = "";

    /// <summary>AK/SK/Bucket 齐全才算可用。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(AccessKeySecret) &&
        !string.IsNullOrWhiteSpace(BucketName);

    /// <summary>
    /// 区域名：从 oss-cn-beijing.aliyuncs.com 推出 cn-beijing。
    /// 内网 Endpoint（oss-cn-beijing-internal）去掉 -internal 后缀，因为公开 URL 必须走公网域名。
    /// </summary>
    public string Region
    {
        get
        {
            var endpoint = (Endpoint ?? "").Trim();
            var host = endpoint.Contains("://") ? new Uri(endpoint).Host : endpoint;
            var region = host.Replace(".aliyuncs.com", "").Replace("oss-", "");
            return region.EndsWith("-internal", StringComparison.OrdinalIgnoreCase)
                ? region[..^"-internal".Length]
                : region;
        }
    }

    /// <summary>拼接对象公开访问 URL。UrlPrefix 优先（自定义域名 / CDN）。</summary>
    public string BuildObjectUrl(string objectKey)
    {
        var key = (objectKey ?? "").TrimStart('/');
        if (!string.IsNullOrWhiteSpace(UrlPrefix))
        {
            return UrlPrefix.TrimEnd('/') + "/" + key;
        }

        return $"https://{BucketName}.oss-{Region}.aliyuncs.com/{key}";
    }

    /// <summary>站点 Host（含 bucket），用于简历预览等"只允许本站 OSS"的校验。</summary>
    public string BucketHost => $"{BucketName}.oss-{Region}.aliyuncs.com";

    /// <summary>上传根前缀，规范化后（无首尾斜杠）。</summary>
    public string NormalizedUploadPath => (UploadPath ?? "").Trim('/');
}

/// <summary>
/// OSS 凭证统一出口：优先读数据库动态配置（管理页维护，保存即生效），
/// 缺失字段回退 appsettings 的 Oss 节点 / 环境变量 Oss__*。
/// 支持租户级覆盖：<see cref="ISettingProvider"/> 按 当前租户 → 全局 → 默认 解析，
/// 租户管理员配置只影响本租户，未配置时回退全局 / 配置文件。
/// </summary>
public interface IOssCredentialProvider
{
    Task<OssStorageOptions> GetOptionsAsync();

    /// <summary>创建已配置好的 OSS 客户端（调用方无需判空，未配置时抛业务异常）。</summary>
    OssClient CreateClient(OssStorageOptions options);

    /// <summary>改配置后清缓存，让新 AK/SK 立刻生效。</summary>
    void ClearCache();
}

public class OssCredentialProvider : IOssCredentialProvider, ITransientDependency
{
    private const string CacheKeyPrefix = "KnowledgeHub:OssOptions";

    /// <summary>部署脚本留下的占位符，等同于"未配置"。</summary>
    private static readonly string[] Placeholders =
    {
        "YOUR_OSS_ACCESS_KEY_ID_HERE",
        "YOUR_OSS_ACCESS_KEY_SECRET_HERE",
    };

    private readonly ISettingProvider _settingProvider;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentTenant _currentTenant;

    public OssCredentialProvider(
        ISettingProvider settingProvider,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ICurrentTenant currentTenant)
    {
        _settingProvider = settingProvider;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _currentTenant = currentTenant;
    }

    /// <summary>缓存按租户隔离，避免串租户复用别家的 Bucket / AK。</summary>
    private string CacheKey =>
        _currentTenant.Id is { } tenantId ? $"{CacheKeyPrefix}:{tenantId}" : CacheKeyPrefix;

    public async Task<OssStorageOptions> GetOptionsAsync()
    {
        var cacheKey = CacheKey;
        if (_memoryCache.TryGetValue(cacheKey, out OssStorageOptions? cached) && cached != null)
        {
            return cached;
        }

        var options = new OssStorageOptions
        {
            Endpoint = await ResolveAsync(KnowledgeHubSettings.OssEndpoint, "Oss:Endpoint", "oss-cn-beijing.aliyuncs.com"),
            AccessKeyId = await ResolveAsync(KnowledgeHubSettings.OssAccessKeyId, "Oss:AccessKeyId", ""),
            AccessKeySecret = await ResolveAsync(KnowledgeHubSettings.OssAccessKeySecret, "Oss:AccessKeySecret", ""),
            BucketName = await ResolveAsync(KnowledgeHubSettings.OssBucketName, "Oss:BucketName", ""),
            UploadPath = await ResolveAsync(KnowledgeHubSettings.OssUploadPath, "Oss:UploadPath", "knowledgehub"),
            UrlPrefix = await ResolveAsync(KnowledgeHubSettings.OssUrlPrefix, "Oss:UrlPrefix", ""),
        };

        _memoryCache.Set(cacheKey, options, TimeSpan.FromMinutes(5));
        return options;
    }

    public OssClient CreateClient(OssStorageOptions options)
    {
        if (options is null || !options.IsConfigured)
        {
            throw new UserFriendlyException("阿里云 OSS 尚未配置，请到「管理 → 对象存储设置」填写 Endpoint、Bucket 与 AccessKey。");
        }

        return new OssClient(options.Endpoint, options.AccessKeyId, options.AccessKeySecret);
    }

    public void ClearCache()
    {
        _memoryCache.Remove(CacheKey);
    }

    /// <summary>
    /// 单字段解析：数据库设置（租户 → 全局 → 默认值）→ 配置文件 / 环境变量 → 兜底默认值。
    /// 逐字段回退，便于"只换 AK、沿用配置文件里的 Bucket"这类灰度切换。
    /// </summary>
    private async Task<string> ResolveAsync(string settingName, string configKey, string fallback)
    {
        var value = (await _settingProvider.GetOrNullAsync(settingName))?.Trim();
        if (IsUsable(value))
        {
            return value!;
        }

        var fromConfig = _configuration[configKey]?.Trim();
        if (IsUsable(fromConfig))
        {
            return fromConfig!;
        }

        return IsUsable(fallback) ? fallback : "";
    }

    private static bool IsUsable(string? value)
        => !string.IsNullOrWhiteSpace(value) && !Placeholders.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
