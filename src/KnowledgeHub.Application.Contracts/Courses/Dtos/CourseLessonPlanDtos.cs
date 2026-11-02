using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

public class CourseLessonPlanDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CourseLessonPlanSource Source { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public string? ContentJson { get; set; }
    public Guid? AiTaskId { get; set; }
    public string? CreatorName { get; set; }
}

public class CourseLessonPlanQueryDto : PagedAndSortedResultRequestDto
{
    public Guid? CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public CourseLessonPlanSource? Source { get; set; }
    public string? Filter { get; set; }
}

public class CreateCourseLessonPlanDto
{
    [Required]
    public Guid CourseId { get; set; }

    public Guid? ChapterId { get; set; }

    [StringLength(256)]
    public string? ChapterTitle { get; set; }

    [Required]
    [StringLength(256)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(1024)]
    public string FileUrl { get; set; } = string.Empty;

    [StringLength(256)]
    public string? FileName { get; set; }

    [StringLength(32)]
    public string? FileExtension { get; set; }

    public long? FileSize { get; set; }
}

public class UpdateCourseLessonPlanDto
{
    [Required]
    [StringLength(256)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? ChapterId { get; set; }

    [StringLength(256)]
    public string? ChapterTitle { get; set; }
}
