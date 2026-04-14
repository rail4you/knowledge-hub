using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

public class ChapterDto : EntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public List<ChapterDto> Children { get; set; } = new();
    public List<KnowledgeResourceDto> KnowledgeResources { get; set; } = new();
}

public class CreateUpdateChapterDto
{
    public Guid CourseId { get; set; }
    public Guid? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 章节导入结果 DTO
/// </summary>
public class ChapterImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
