using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using Volo.Abp;

namespace KnowledgeHub;

/// <summary>
/// Qwen（OpenAI 兼容接口）统一客户端工厂。
/// 所有 Qwen 调用共用同一个带全局并发限流的 HttpClient，避免并发请求打爆配额、
/// 触发 429 或拖垮进程。限流参数: Qwen:MaxConcurrentRequests（默认 3）。
/// </summary>
public static class QwenClient
{
    private static readonly object SyncRoot = new();
    private static HttpClient? _httpClient;
    private static ConcurrencyLimiter? _limiter;

    /// <summary>
    /// DI 根（ApplicationModule 初始化时写入）：用于按需创建 scope 解析动态 Key。
    /// 直接从 root 解析会因 scoped 依赖报错，必须经 scope。
    /// </summary>
    public static IServiceScopeFactory? ScopeFactory { get; set; }

    /// <summary>
    /// API Key 解析器（默认读动态配置 → 回退 appsettings）。测试可替换。
    /// </summary>
    public static Func<Task<string>>? ApiKeyResolver { get; set; } = DefaultApiKeyResolver;

    private static async Task<string> DefaultApiKeyResolver()
    {
        if (ScopeFactory == null)
        {
            return null!;
        }

        using var scope = ScopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<Application.AI.IQwenCredentialProvider>();
        return await provider.GetApiKeyAsync();
    }

    /// <summary>
    /// 获取全局 Qwen 限流许可（供非 OpenAI-SDK 的直接 HTTP 调用，如视频理解 VL 复用同一限流）。
    /// 调用方必须释放返回的 lease。
    /// </summary>
    public static async Task<RateLimitLease> AcquireAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        return await GetLimiter(configuration).AcquireAsync(1, ct).ConfigureAwait(false);
    }

    public static async Task<IChatClient> CreateChatClient(IConfiguration configuration, string? modelOverride = null)
    {
        // 动态 Key（管理页可换，换完即生效）→ 回退 appsettings。
        string? apiKey = null;
        if (ApiKeyResolver != null)
        {
            apiKey = await ApiKeyResolver().ConfigureAwait(false);
        }
        apiKey ??= configuration["Qwen:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AbpException("Qwen:ApiKey is not configured");
        }
        var baseUrl = configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = modelOverride
            ?? configuration["Qwen:Model"]
            ?? "qwen-flash";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(baseUrl),
                Transport = new HttpClientPipelineTransport(GetHttpClient(configuration)),
            });

        return openaiClient.GetChatClient(model).AsIChatClient();
    }

    private static HttpClient GetHttpClient(IConfiguration configuration)
    {
        lock (SyncRoot)
        {
            // DelegatingHandler 必须指定 InnerHandler，否则 SendAsync 会抛
            // "The inner handler has not been assigned."（后台任务/流式生成全挂）。
            return _httpClient ??= new HttpClient(new QwenRateLimitingHandler(GetLimiter(configuration))
            {
                InnerHandler = new SocketsHttpHandler(),
            })
            {
                // LLM 长响应/流式：给足超时
                Timeout = TimeSpan.FromMinutes(10),
            };
        }
    }

    private static ConcurrencyLimiter GetLimiter(IConfiguration configuration)
    {
        lock (SyncRoot)
        {
            if (_limiter != null)
            {
                return _limiter;
            }

            var max = Math.Max(1, configuration.GetValue("Qwen:MaxConcurrentRequests", 3));
            _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = max,
                QueueLimit = 1000,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
            return _limiter;
        }
    }

    /// <summary>把全局 Qwen 并发闸门挂到 HttpClient 管线上。</summary>
    private sealed class QwenRateLimitingHandler : DelegatingHandler
    {
        private readonly RateLimiter _limiter;

        public QwenRateLimitingHandler(RateLimiter limiter)
        {
            _limiter = limiter;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var lease = await _limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                throw new HttpRequestException("Qwen 并发限流：未能获取许可");
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
