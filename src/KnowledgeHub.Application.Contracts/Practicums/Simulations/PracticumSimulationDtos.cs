using System;
using KnowledgeHub.Practicums.Enums;

namespace KnowledgeHub.Practicums.Simulations;

public class PracticumSimulationDto : Volo.Abp.Application.Dtos.EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string EntryPath { get; set; } = "index.html";
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public PracticumSimulationStatus Status { get; set; }
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public bool CoopCoepRequired { get; set; }
    public int SortOrder { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    /// <summary>内容哈希（同一构建被重复上传时为相同值；历史数据为空）</summary>
    public string? ContentHash { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreatePracticumSimulationDto
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public string? EntryPath { get; set; }
    public string UploadedFilePath { get; set; } = string.Empty;
}

public class UpdatePracticumSimulationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public int SortOrder { get; set; }
}
