namespace KnowledgeHub.Branding;

/// <summary>站点品牌（全局唯一，三端共用）</summary>
public class BrandingDto
{
    public string AppTitle { get; set; } = "易课通";

    public string AppSubtitle { get; set; } = "知识资源库";

    public string FooterText { get; set; } = "© 2026 易课通 · 知识资源库";

    /// <summary>Logo 图片 URL；为空时前端显示“易”字文字 Logo</summary>
    public string LogoUrl { get; set; } = "";
}
