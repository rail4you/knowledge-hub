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
}