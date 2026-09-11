using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源生成物登记：记录某个资源版本产出的衍生文件（缩略图 / 预览 PDF 等），
/// 供前端直接消费与删除/换版本时统一清理。
/// </summary>
public class ResourceArtifact : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ResourceId { get; set; }

    public Guid ResourceVersionId { get; set; }

    public ResourceArtifactKind Kind { get; set; }

    /// <summary>同一 Kind 的变体标识，如 w400（缩略图宽度）或 full。</summary>
    public string Variant { get; set; } = string.Empty;

    /// <summary>相对文件存储根目录的路径（与 Resource.FilePath 口径一致）。</summary>
    public string FilePath { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? SizeBytes { get; set; }

    public ResourceArtifactState State { get; set; } = ResourceArtifactState.Ready;

    public string? ErrorMessage { get; set; }

    public DateTime GeneratedAt { get; set; }

    protected ResourceArtifact()
    {
    }

    public ResourceArtifact(Guid id, Guid resourceId, Guid resourceVersionId, ResourceArtifactKind kind, string variant)
        : base(id)
    {
        ResourceId = resourceId;
        ResourceVersionId = resourceVersionId;
        Kind = kind;
        Variant = variant;
    }
}

public enum ResourceArtifactKind : byte
{
    /// <summary>列表封面缩略图（图片缩放 / 视频抽帧）。</summary>
    Thumbnail = 0,
    /// <summary>Office 转换后的整份 PDF 预览。</summary>
    PreviewPdf = 10
}

public enum ResourceArtifactState : byte
{
    Ready = 0,
    Failed = 40
}
