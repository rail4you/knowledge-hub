using System;
using System.Net.Http;
using System.Threading.Tasks;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// 生成结果持久化：把通义万相返回的临时 URL 转存到本地存储，避免 DashScope URL 过期后无法访问。
/// 转存失败时回退返回原始 URL，保证主流程不中断。
/// </summary>
public interface IMediaStorageService
{
    Task<string> PersistAsync(string sourceUrl, string fileExtension, string subDir);
}

public class MediaStorageService : IMediaStorageService, ITransientDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileStorageService _fileStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaStorageService> _logger;

    public MediaStorageService(
        IHttpClientFactory httpClientFactory,
        IFileStorageService fileStorage,
        IConfiguration configuration,
        ILogger<MediaStorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fileStorage = fileStorage;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> PersistAsync(string sourceUrl, string fileExtension, string subDir)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return sourceUrl;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(sourceUrl);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var fileName = $"{Guid.NewGuid():N}{fileExtension}";
            var path = await _fileStorage.SaveAsync(stream, fileName, $"ai-media/{subDir}");
            var relativeUrl = _fileStorage.GetFileUrl(path);

            var selfUrl = (_configuration["App:SelfUrl"] ?? string.Empty).TrimEnd('/');
            return string.IsNullOrEmpty(selfUrl) ? relativeUrl : selfUrl + relativeUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "持久化生成媒体失败，回退原始 URL：{Url}", sourceUrl);
            return sourceUrl;
        }
    }
}
