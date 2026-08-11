using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using KnowledgeHub.Resources.Enums;

namespace KnowledgeHub.Courses.Dtos;

public class CourseResourceDto : EntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid ResourceId { get; set; }
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreationTime { get; set; }

    // Resource 字段
    public string? ResourceName { get; set; }
    public string? Description { get; set; }
    public string? FilePath { get; set; }
    public string? Keywords { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public ResourceType ResourceType { get; set; }
    public bool IsDownloadable { get; set; }
}

public class CreateCourseResourceDto
{
    [Required]
    public Guid CourseId { get; set; }

    [Required]
    public Guid ResourceId { get; set; }

    /// <summary>可选，覆盖默认显示名</summary>
    [StringLength(256)]
    public string? DisplayName { get; set; }

    public int SortOrder { get; set; }
}
