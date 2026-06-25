using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using KnowledgeHub.Resources.Enums;

namespace KnowledgeHub.Courses.Dtos;

public class ChapterResourceDto : EntityDto<Guid>
{
    public Guid ChapterId { get; set; }
    public Guid ResourceId { get; set; }
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }

    // Resource 字段
    public string? ResourceName { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public ResourceType ResourceType { get; set; }
    public bool IsDownloadable { get; set; }
}

public class CreateChapterResourceDto
{
    [Required]
    public Guid ChapterId { get; set; }

    [Required]
    public Guid ResourceId { get; set; }

    /// <summary>可选，覆盖默认显示名</summary>
    [StringLength(256)]
    public string? DisplayName { get; set; }

    public int SortOrder { get; set; }
}
