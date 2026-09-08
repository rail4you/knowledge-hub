using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Branding;

public class UpdateBrandingDto
{
    [Required]
    [StringLength(32)]
    public string AppTitle { get; set; } = "易课通";

    [StringLength(64)]
    public string AppSubtitle { get; set; } = "知识资源库";

    [StringLength(256)]
    public string FooterText { get; set; } = "© 2026 易课通 · 知识资源库";

    /// <summary>Logo 图片 URL（OSS 上传后填入）；为空表示使用文字 Logo</summary>
    [StringLength(512)]
    public string? LogoUrl { get; set; }
}
