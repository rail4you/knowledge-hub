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

    private const string AiPrefix = Prefix + ".Ai";

    /// <summary>Qwen API Key（全局唯一，加密存储，AI 使用管理页维护）。</summary>
    public const string QwenApiKey = AiPrefix + ".QwenApiKey";

    /// <summary>AI 每日配额（JSON：角色 → 分组 → 次数，null/缺失=不限）。</summary>
    public const string AiQuotas = AiPrefix + ".Quotas";

    /// <summary>默认配额：学生职业规划 2/天、聊天 30/天；教师职业规划 5/天、聊天 100/天；其余不限。</summary>
    public const string DefaultAiQuotas =
        "{\"Student\":{\"CareerGuidance\":2,\"Chat\":30},\"Teacher\":{\"CareerGuidance\":5,\"Chat\":100}}";
}