using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Aliyun.OSS;
using KnowledgeHub.Permissions;
using KnowledgeHub.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace KnowledgeHub.Resources.Oss;

/// <summary>
/// 阿里云 OSS 设置：读写当前租户（host 上下文写全局）的 Endpoint / AK / SK / Bucket / 上传前缀，
/// 支持"写一个探测对象再删掉"的真实连通性测试。
/// AK/SK 经 SettingDefinition(isEncrypted) 加密存储，从不回传明文。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize(KnowledgeHubPermissions.Oss.Default)]
public class OssConfigAppService : KnowledgeHubAppService, IOssConfigAppService
{
    private const string SourceDatabase = "database";
    private const string SourceConfig = "config";
    private const string SourceNone = "none";

    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IOssCredentialProvider _credentials;

    public OssConfigAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IOssCredentialProvider credentials)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _credentials = credentials;
    }

    public async Task<OssConfigDto> GetAsync()
    {
        var options = await _credentials.GetOptionsAsync();

        return new OssConfigDto
        {
            Endpoint = options.Endpoint,
            AccessKeyId = options.AccessKeyId,
            HasAccessKeySecret = !string.IsNullOrWhiteSpace(options.AccessKeySecret),
            BucketName = options.BucketName,
            UploadPath = options.NormalizedUploadPath,
            UrlPrefix = options.UrlPrefix,
            IsConfigured = options.IsConfigured,
            Source = await ResolveSourceAsync(options),
            PublicHost = options.IsConfigured
                ? (string.IsNullOrWhiteSpace(options.UrlPrefix)
                    ? options.BucketHost
                    : options.UrlPrefix.TrimEnd('/'))
                : "",
        };
    }

    public async Task<OssConfigDto> UpdateAsync(UpdateOssConfigDto input)
    {
        var endpoint = (input.Endpoint ?? "").Trim().TrimEnd('/');
        var bucketName = (input.BucketName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(bucketName))
        {
            throw new UserFriendlyException("Endpoint 与 Bucket 名称不能为空。");
        }

        // Endpoint 允许带 https:// 前缀入库，但解析 Region 时会归一化，这里统一存干净值。
        if (endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = endpoint["https://".Length..];
        }

        await SetAsync(KnowledgeHubSettings.OssEndpoint, endpoint);
        await SetAsync(KnowledgeHubSettings.OssAccessKeyId, (input.AccessKeyId ?? "").Trim());
        await SetAsync(KnowledgeHubSettings.OssBucketName, bucketName);
        await SetAsync(KnowledgeHubSettings.OssUploadPath, (input.UploadPath ?? "").Trim().Trim('/'));
        await SetAsync(KnowledgeHubSettings.OssUrlPrefix, (input.UrlPrefix ?? "").Trim().TrimEnd('/'));

        // 密钥：为空表示保持不变（换 AK 时可只填 ID），ClearAccessKeySecret 显式清空。
        var secret = (input.AccessKeySecret ?? "").Trim();
        if (input.ClearAccessKeySecret)
        {
            await SetAsync(KnowledgeHubSettings.OssAccessKeySecret, "");
        }
        else if (!string.IsNullOrEmpty(secret))
        {
            await SetAsync(KnowledgeHubSettings.OssAccessKeySecret, secret);
        }

        // 保存即生效：清掉本租户的凭证缓存（OssClient 每次新建，无需重启 API）。
        _credentials.ClearCache();

        return await GetAsync();
    }

    /// <summary>
    /// 连通性测试：写入 1 字节探测对象后立即删除。
    /// 用 PutObject/DeleteObject 而不是 ListBuckets/Bucket 查询 —— RAM 策略只需授最小权限即可通过，
    /// 同时真实验证"能写能删"，避免出现"能列目录但传不上去"的假成功。
    /// </summary>
    public async Task<OssTestResultDto> TestAsync()
    {
        OssStorageOptions options;
        try
        {
            options = await _credentials.GetOptionsAsync();
        }
        catch (Exception ex)
        {
            return new OssTestResultDto { Success = false, Error = ex.Message };
        }

        if (!options.IsConfigured)
        {
            return new OssTestResultDto { Success = false, Error = "OSS 尚未配置完整（缺少 Bucket 或 AccessKey）。" };
        }

        var objectKey = $"{options.NormalizedUploadPath}/.healthcheck/probe-{DateTime.Now:yyyyMMddHHmmssfff}.txt";
        try
        {
            var client = _credentials.CreateClient(options);
            var bytes = Encoding.UTF8.GetBytes("knowledgehub oss health check");

            await Task.Run(() =>
            {
                client.PutObject(
                    options.BucketName,
                    objectKey,
                    new MemoryStream(bytes),
                    new ObjectMetadata { ContentType = "text/plain" });
            });

            try
            {
                await Task.Run(() => client.DeleteObject(options.BucketName, objectKey));
            }
            catch
            {
                // 删除失败不影响"可写"的判定，只提示残留对象。
            }

            return new OssTestResultDto
            {
                Success = true,
                ProbeUrl = options.BuildObjectUrl(objectKey),
            };
        }
        catch (Exception ex)
        {
            return new OssTestResultDto { Success = false, Error = DescribeOssError(ex) };
        }
    }

    /// <summary>把 OSS SDK 异常翻译成可操作的中文提示（含最常见的 RAM / Bucket 权限问题）。</summary>
    private static string DescribeOssError(Exception ex)
    {
        var message = ex.Message;

        if (message.Contains("SignatureDoesNotMatch", StringComparison.OrdinalIgnoreCase))
        {
            return "AccessKeyId 与 AccessKeySecret 不匹配，或 Endpoint 与 AK 所属账号的区域不一致。";
        }

        if (message.Contains("InvalidAccessKeyId", StringComparison.OrdinalIgnoreCase))
        {
            return "AccessKeyId 无效，请确认该 AK 属于当前阿里云账号且状态为「启用」。";
        }

        if (message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("403", StringComparison.Ordinal))
        {
            return "没有该 Bucket 的写入权限（403）。请给 RAM 用户授予 oss:PutObject / oss:DeleteObject 等策略，"
                   + "并确认 Bucket 名称与所属账号正确。";
        }

        if (message.Contains("NoSuchBucket", StringComparison.OrdinalIgnoreCase))
        {
            return "Bucket 不存在或不属于当前账号，请检查 Bucket 名称与区域。";
        }

        if (message.Contains("SignatureExpired", StringComparison.OrdinalIgnoreCase))
        {
            return "本机与服务器时间偏差过大导致签名过期，请校准系统时间（NTP）后重试。";
        }

        return message;
    }

    /// <summary>判断当前生效配置来自数据库还是配置文件（前端提示用）。</summary>
    private async Task<string> ResolveSourceAsync(OssStorageOptions options)
    {
        if (!options.IsConfigured)
        {
            return SourceNone;
        }

        var fromDb = new[]
        {
            KnowledgeHubSettings.OssAccessKeyId,
            KnowledgeHubSettings.OssAccessKeySecret,
            KnowledgeHubSettings.OssBucketName,
        };

        foreach (var name in fromDb)
        {
            var value = await _settingProvider.GetOrNullAsync(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return SourceDatabase;
            }
        }

        return SourceConfig;
    }

    /// <summary>host 上下文写全局（所有租户的回退配置），租户上下文只写本租户。</summary>
    private Task SetAsync(string name, string value)
    {
        return CurrentTenant.Id.HasValue
            ? _settingManager.SetForTenantAsync(CurrentTenant.Id.Value, name, value)
            : _settingManager.SetGlobalAsync(name, value);
    }
}
