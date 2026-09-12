using System;
using System.Threading.Tasks;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// Qwen API Key 统一出口：优先读数据库动态配置（AI 使用管理页维护，
/// 修改即生效），未配置时回退 appsettings。
/// 支持租户级 Key：<see cref="ISettingProvider"/> 按 当前租户 → 全局 → 默认 解析，
/// 即租户管理员配置的 Key 只影响本租户，未配置时回退全局/配置文件。
/// </summary>
public interface IQwenCredentialProvider
{
    Task<string> GetApiKeyAsync();

    /// <summary>换 Key 后清缓存，让新 Key 立刻生效。</summary>
    void ClearCache();
}

public class QwenCredentialProvider : IQwenCredentialProvider, ITransientDependency
{
    private const string CacheKeyPrefix = "KnowledgeHub:QwenApiKey";
    private const string Placeholder = "YOUR_QWEN_API_KEY_HERE";

    private readonly ISettingProvider _settingProvider;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentTenant _currentTenant;

    public QwenCredentialProvider(
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

    /// <summary>缓存按租户隔离，避免串租户复用别家的 Key。</summary>
    private string CacheKey =>
        _currentTenant.Id is { } tenantId ? $"{CacheKeyPrefix}:{tenantId}" : CacheKeyPrefix;

    public async Task<string> GetApiKeyAsync()
    {
        var cacheKey = CacheKey;
        if (_memoryCache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached!;
        }

        // ISettingProvider 解析顺序：当前租户 → 全局 → 默认值
        var key = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.QwenApiKey);
        if (string.IsNullOrWhiteSpace(key) || key.Trim() == Placeholder)
        {
            key = _configuration["Qwen:ApiKey"];
        }

        if (string.IsNullOrWhiteSpace(key) || key.Trim() == Placeholder)
        {
            throw new AbpException("Qwen:ApiKey is not configured");
        }

        key = key.Trim();
        _memoryCache.Set(cacheKey, key, TimeSpan.FromMinutes(5));
        return key;
    }

    public void ClearCache()
    {
        _memoryCache.Remove(CacheKey);
    }
}
