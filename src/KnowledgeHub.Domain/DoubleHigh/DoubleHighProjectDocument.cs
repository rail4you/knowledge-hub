using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

/// <summary>
/// 项目依据文件（政策文件、评估办法、通知等）。
///
/// <para>
/// 项目级「依据文件」可上传多个；指标的「依据」既可以手写文本，
/// 也可以直接引用项目里的某个依据文件（<see cref="DoubleHighIndicator.BasisDocumentId"/>），
/// 引用后填报页可点击查看 / 预览原件。</para>
/// </summary>
public class DoubleHighProjectDocument : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属评估项目。</summary>
    public Guid ProjectId { get; set; }

    /// <summary>显示名称（默认取文件名）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>说明（如文件出处、文号）。</summary>
    public string? Description { get; set; }

    /// <summary>原始文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>公开访问地址（OSS 直链；本地存储时为空）。</summary>
    public string? FileUrl { get; set; }

    /// <summary>存储路径：<c>oss:{objectKey}</c> / <c>local:{relativePath}</c>。</summary>
    public string? StoredPath { get; set; }

    public int SortOrder { get; set; }

    public DoubleHighProjectDocument()
    {
    }

    public DoubleHighProjectDocument(Guid id, Guid projectId, string title)
        : base(id)
    {
        ProjectId = projectId;
        Title = title;
    }
}
