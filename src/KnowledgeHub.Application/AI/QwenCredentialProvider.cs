using System;
using System.Threading.Tasks;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// Qwen API Key 统一出口：优先读数据库动态配置（AI 使用管理页维护，
/// 修改即生效），未配置时回退 appsettings。
/// </summary>
public interface IQwenCredentialProvider
{
    Task<string> GetApiKeyAsync();

    /// <summary>换 Key 后清缓存，让新 Key 立刻生效。</summary>
    void ClearCache();
}

public class QwenCredentialProvider : IQwenCredentialProvider, ITransientDependency
{
    private const string CacheKey = "KnowledgeHub:QwenApiKey";
    private const string Placeholder = "YOUR_QWEN_API_KEY_HERE";

    private readonly ISettingProvider _settingProvider;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;

    public QwenCredentialProvider(
        ISettingProvider settingProvider,
        IConfiguration configuration,
        IMemoryCache memoryCache)
    {
        _settingProvider = settingProvider;
        _configuration = configuration;
        _memoryCache = memoryCache;
    }

    public async Task<string> GetApiKeyAsync()
    {
        if (_memoryCache.TryGetValue(CacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached!;
        }

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
        _memoryCache.Set(CacheKey, key, TimeSpan.FromMinutes(5));
        return key;
    }

    public void ClearCache()
    {
        _memoryCache.Remove(CacheKey);
    }
}
