using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Resources.Oss;

/// <summary>OSS 连接配置（租户级；host 上下文读写全局）。</summary>
public class OssConfigDto
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>是否已配置 AccessKeySecret（密钥从不回传明文）。</summary>
    public bool HasAccessKeySecret { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string UploadPath { get; set; } = string.Empty;

    /// <summary>自定义域名 / CDN 加速前缀，可空。</summary>
    public string UrlPrefix { get; set; } = string.Empty;

    /// <summary>AK/SK/Bucket 齐全（上传可用）。</summary>
    public bool IsConfigured { get; set; }

    /// <summary>生效来源：database=数据库设置，config=配置文件回退，none=未配置。</summary>
    public string Source { get; set; } = "none";

    /// <summary>按当前配置推导的公开访问域名，便于核对自定义域名是否生效。</summary>
    public string PublicHost { get; set; } = string.Empty;
}

public class UpdateOssConfigDto
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>AccessKeySecret；为空表示保持原值不变。</summary>
    public string? AccessKeySecret { get; set; }

    /// <summary>为 true 时清空密钥（与改密钥互斥）。</summary>
    public bool ClearAccessKeySecret { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string UploadPath { get; set; } = "knowledgehub";

    public string UrlPrefix { get; set; } = string.Empty;
}

public class OssTestResultDto
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    /// <summary>探测对象写入后的公开 URL，用于人工核对自定义域名 / 防盗链设置。</summary>
    public string? ProbeUrl { get; set; }
}

/// <summary>
/// 阿里云 OSS 对象存储设置：读写当前租户（host 上下文写全局）的连接配置并支持连通性测试。
/// AccessKeyId / AccessKeySecret 加密存储（SettingDefinition isEncrypted），从不回传明文。
/// </summary>
public interface IOssConfigAppService : IApplicationService
{
    /// <summary>获取当前生效的 OSS 配置。</summary>
    Task<OssConfigDto> GetAsync();

    /// <summary>保存 OSS 配置（写当前租户；host 上下文写全局）。</summary>
    Task<OssConfigDto> UpdateAsync(UpdateOssConfigDto input);

    /// <summary>写入并删除一个探测对象，验证 AK/SK/Bucket 是否真的可用。</summary>
    Task<OssTestResultDto> TestAsync();
}
