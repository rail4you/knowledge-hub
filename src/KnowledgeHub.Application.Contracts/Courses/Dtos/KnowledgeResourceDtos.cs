using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

public class KnowledgeResourceDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public int Difficulty { get; set; }
    public int SortOrder { get; set; }
    public string? Tags { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ResourceId { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public List<KnowledgeResourceDto> Children { get; set; } = new();
}

public class CreateUpdateKnowledgeResourceDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public int Difficulty { get; set; } = 1;
    public int SortOrder { get; set; }
    public string? Tags { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ResourceId { get; set; }
}
