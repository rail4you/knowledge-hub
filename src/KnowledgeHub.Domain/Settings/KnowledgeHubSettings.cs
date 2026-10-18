namespace KnowledgeHub.Settings;

public static class KnowledgeHubSettings
{
    private const string Prefix = "KnowledgeHub";
    private const string InstallPrefix = Prefix + ".Install";

    public const string IsInstalled = InstallPrefix + ".IsInstalled";
    public const string InstalledEdition = InstallPrefix + ".InstalledEdition";
    public const string LicenseKey = InstallPrefix + ".LicenseKey";

    private const string BrandingPrefix = Prefix + ".Branding";

    /// <summary>应用标题（默认：易课通）</summary>
    public const string BrandingAppTitle = BrandingPrefix + ".AppTitle";

    /// <summary>应用副标题（默认：知识资源库）</summary>
    public const string BrandingAppSubtitle = BrandingPrefix + ".AppSubtitle";

    /// <summary>三端统一页脚文本（默认：© 2026 易课通 · 知识资源库）</summary>
    public const string BrandingFooterText = BrandingPrefix + ".FooterText";

    /// <summary>Logo 图片 URL（为空时显示“易”字文字 Logo）</summary>
    public const string BrandingLogoUrl = BrandingPrefix + ".LogoUrl";

    private const string SmtpPrefix = Prefix + ".Smtp";

    /// <summary>SMTP 服务器地址（如 smtp.163.com）。</summary>
    public const string SmtpHost = SmtpPrefix + ".Host";

    /// <summary>SMTP 端口（默认 465，SSL）。</summary>
    public const string SmtpPort = SmtpPrefix + ".Port";

    /// <summary>SMTP 登录账号。</summary>
    public const string SmtpUserName = SmtpPrefix + ".UserName";

    /// <summary>SMTP 密码/授权码（加密存储）。</summary>
    public const string SmtpPassword = SmtpPrefix + ".Password";

    /// <summary>发件人地址（为空时回退登录账号）。</summary>
    public const string SmtpFromAddress = SmtpPrefix + ".FromAddress";

    /// <summary>发件人显示名。</summary>
    public const string SmtpFromName = SmtpPrefix + ".FromName";

    private const string StoragePrefix = Prefix + ".Storage";

    /// <summary>默认单租户存储配额（字节，默认 50GB）。租户未单独配置时回退到此值。</summary>
    public const string DefaultStorageQuotaBytes = StoragePrefix + ".DefaultQuotaBytes";

    /// <summary>租户级存储配额（字节），0 = 不限。host 管理员在监控页按租户写入。</summary>
    public const string StorageQuotaBytes = StoragePrefix + ".QuotaBytes";

    /// <summary>配额预警阈值（百分比，默认 80）。</summary>
    public const string StorageWarnPercent = StoragePrefix + ".WarnPercent";

    private const string OssPrefix = Prefix + ".Oss";

    /// <summary>OSS Endpoint（如 oss-cn-beijing.aliyuncs.com，不含 bucket 前缀）。</summary>
    public const string OssEndpoint = OssPrefix + ".Endpoint";

    /// <summary>OSS AccessKeyId（RAM 子账号 AK，加密存储，仅后端可读）。</summary>
    public const string OssAccessKeyId = OssPrefix + ".AccessKeyId";

    /// <summary>OSS AccessKeySecret（加密存储，仅后端可读，从不回传明文）。</summary>
    public const string OssAccessKeySecret = OssPrefix + ".AccessKeySecret";

    /// <summary>OSS Bucket 名称。</summary>
    public const string OssBucketName = OssPrefix + ".BucketName";

    /// <summary>OSS 上传路径前缀（对象键统一以此开头）。</summary>
    public const string OssUploadPath = OssPrefix + ".UploadPath";

    /// <summary>公开访问地址前缀（自定义域名/CDN 加速域名，留空则按 endpoint+bucket 推导）。</summary>
    public const string OssUrlPrefix = OssPrefix + ".UrlPrefix";

    private const string AiPrefix = Prefix + ".Ai";

    /// <summary>Qwen API Key（全局唯一，加密存储，AI 使用管理页维护）。</summary>
    public const string QwenApiKey = AiPrefix + ".QwenApiKey";

    /// <summary>AI 每日配额（JSON：角色 → 分组 → 次数，null/缺失=不限）。</summary>
    public const string AiQuotas = AiPrefix + ".Quotas";

    /// <summary>默认配额：学生职业规划 2/天、聊天 30/天；教师职业规划 5/天、聊天 100/天、图片 20/天、视频 5/天；其余不限。</summary>
    public const string DefaultAiQuotas =
        "{\"Student\":{\"CareerGuidance\":2,\"Chat\":30},\"Teacher\":{\"CareerGuidance\":5,\"Chat\":100,\"ImageGeneration\":20,\"VideoGeneration\":5}}";
}