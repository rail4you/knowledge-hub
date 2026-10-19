using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumSimulation : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string EntryPath { get; set; } = "index.html";
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public PracticumSimulationStatus Status { get; set; }
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    /// <summary>
    /// 解压后文件树的内容哈希（SHA-256，十六进制）。
    /// 同一份 Unity 构建被多次上传时，载荷只存一份 <root>/_blobs/&lt;hash&gt;/，
    /// 各 slug 目录用符号链接指向它，避免 N 份完整副本。
    /// 历史数据（本列上线前导入的）为 NULL，此时按传统独立目录处理。
    /// </summary>
    public string? ContentHash { get; set; }

    public bool CoopCoepRequired { get; set; } = true;
    public int SortOrder { get; set; }

    public PracticumSimulation()
    {
    }

    public PracticumSimulation(Guid id, Guid projectId, string name, string slug)
        : base(id)
    {
        ProjectId = projectId;
        Name = name;
        Slug = slug;
    }
}
