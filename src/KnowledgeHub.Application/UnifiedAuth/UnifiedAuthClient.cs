using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.UnifiedAuth;

/// <summary>
/// 账户服务 HTTP 客户端。负责 client_credentials 令牌缓存与各接口调用。
/// </summary>
public class UnifiedAuthClient : ITransientDependency
{
    public const string HttpClientName = "UnifiedAccount";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<UnifiedAuthOptions> _options;
    private readonly ILogger<UnifiedAuthClient> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt;

    public UnifiedAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<UnifiedAuthOptions> options,
        ILogger<UnifiedAuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public bool Enabled => _options.Value.Enabled;

    public async Task<UnifiedVerifyResponse> VerifyAsync(
        Guid appTenantId, string identifier, string password, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/v1/auth/verify", new
        {
            app_tenant_id = appTenantId.ToString(),
            identifier,
            password
        }, authorized: true, cancellationToken);

        if (response.Status is HttpStatusCode.OK or HttpStatusCode.Unauthorized)
        {
            return Deserialize<UnifiedVerifyResponse>(response.Body)
                   ?? new UnifiedVerifyResponse { Allowed = false, Reason = "invalid_response" };
        }

        throw BuildException(response);
    }

    public async Task<UnifiedCreateMemberResponse> CreateMemberAsync(
        CreateUnifiedMemberRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/v1/members", request, authorized: true, cancellationToken);
        if (!response.IsSuccess)
        {
            throw BuildException(response);
        }
        return Deserialize<UnifiedCreateMemberResponse>(response.Body)
               ?? throw new UnifiedAuthException((int)response.Status, "账户服务返回内容无法解析");
    }

    public async Task ChangePasswordAsync(
        Guid appTenantId, string identifier, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/v1/app-passwords:change", new
        {
            app_tenant_id = appTenantId.ToString(),
            identifier,
            current_password = currentPassword,
            new_password = newPassword
        }, authorized: true, cancellationToken);

        if (!response.IsSuccess)
        {
            throw BuildException(response);
        }
    }

    public async Task LinkIdentityAsync(
        Guid appTenantId, Guid personId, string externalId, string externalLogin,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/v1/app-identities", new
        {
            person_id = personId.ToString(),
            app_tenant_id = appTenantId.ToString(),
            external_id = externalId,
            external_login = externalLogin
        }, authorized: true, cancellationToken);

        if (!response.IsSuccess)
        {
            throw BuildException(response);
        }
    }

    public async Task<UnifiedLookupTenantResponse?> LookupTenantAsync(
        string name, CancellationToken cancellationToken = default)
    {
        var query = "?name=" + Uri.EscapeDataString(name ?? string.Empty);
        var response = await SendAsync(HttpMethod.Get, "/v1/tenants/lookup" + query, null, authorized: true, cancellationToken);
        if (!response.IsSuccess)
        {
            throw BuildException(response);
        }
        return Deserialize<UnifiedLookupTenantResponse>(response.Body);
    }

    public async Task RegisterTenantAsync(
        string appTenantId, string name, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/v1/tenants", new
        {
            app_tenant_id = appTenantId,
            name
        }, authorized: true, cancellationToken);
        if (!response.IsSuccess)
        {
            throw BuildException(response);
        }
    }

    // ── 内部 ────────────────────────────────────────────────

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.Value.TimeoutSeconds));
        return client;
    }

    private string BuildUrl(string path)
    {
        var baseUrl = (_options.Value.BaseUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("UnifiedAuth:BaseUrl 未配置");
        }
        return baseUrl + path;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _accessToken;
            }

            var options = _options.Value;
            using var client = CreateClient();
            var payload = JsonSerializer.Serialize(new
            {
                grant_type = "client_credentials",
                client_id = options.ClientId,
                client_secret = options.ClientSecret
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(BuildUrl("/v1/oauth/token"), content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("统一账户服务获取令牌失败: {Status} {Body}", (int)response.StatusCode, body);
                throw new UnifiedAuthException((int)response.StatusCode, "账户服务 client 凭证无效");
            }

            using var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var el) ? el.GetInt32() : 3600;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnifiedAuthException(500, "账户服务未返回 access_token");
            }
            _accessToken = token;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
            return _accessToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpResult> SendAsync(
        HttpMethod method, string path, object? body, bool authorized, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(method, BuildUrl(path));
        if (authorized)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", await GetTokenAsync(cancellationToken));
        }
        if (body != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResult(response.StatusCode, text);
    }

    private static T? Deserialize<T>(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static UnifiedAuthException BuildException(HttpResult result)
    {
        var error = Deserialize<UnifiedErrorResponse>(result.Body);
        var message = error?.Message ?? error?.Error ?? $"账户服务返回 {(int)result.Status}";
        return new UnifiedAuthException((int)result.Status, message);
    }

    private readonly record struct HttpResult(HttpStatusCode Status, string Body)
    {
        public bool IsSuccess => (int)Status is >= 200 and < 300;
    }
}
