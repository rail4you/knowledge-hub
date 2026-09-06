using System;
using System.Collections.Generic;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.SpecialEducation.Dtos;

public class TeachingSectionItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Activities { get; set; } = new();
}

public class SpecialTeachingDesignDto : FullAuditedEntityDto<Guid>
{
    public SpecialEduCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public Guid? ResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int Duration { get; set; }
    public List<string> Objectives { get; set; } = new();
    public List<string> KeyPoints { get; set; } = new();
    public List<string> Difficulties { get; set; } = new();
    public List<TeachingSectionItemDto> Sections { get; set; } = new();
    public List<string> Methods { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public List<string> Assessment { get; set; } = new();
    public List<string> Homework { get; set; } = new();
    public List<string> BoardDesign { get; set; } = new();
    public List<string> SlidesOutline { get; set; } = new();
    public List<string> Activities { get; set; } = new();
    public List<string> AssessmentTools { get; set; } = new();
    public string StandardBasis { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public SpecialEduPlanStatus Status { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerName { get; set; }
}

public class GenerateTeachingDesignInputDto
{
    public SpecialEduCategory Category { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int Duration { get; set; } = 45;
    public string Objectives { get; set; } = string.Empty;
    public string StudentTraits { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? CustomPrompt { get; set; }
}

public class SaveTeachingDesignInputDto
{
    public Guid? Id { get; set; }
    public SpecialEduCategory Category { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ResourceId { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public string SourceInputJson { get; set; } = "{}";
}

public class ReviewTeachingDesignInputDto
{
    public Guid Id { get; set; }
    public bool Approved { get; set; }
    public string? Comment { get; set; }
}

public class SubmitTeachingDesignForReviewInputDto
{
    public Guid Id { get; set; }
    /// <summary>指派的审核教师（租户内教师）。为空则仅提交待审核。</summary>
    public Guid? ReviewerUserId { get; set; }
}

public class GetTeachingDesignListInputDto : PagedAndSortedResultRequestDto
{
    public SpecialEduCategory? Category { get; set; }
    public SpecialEduPlanStatus? Status { get; set; }
    public string? Keyword { get; set; }
}

public class ExportTeachingDesignInputDto
{
    public string ResultJson { get; set; } = string.Empty;
}

public class ChatMessageChunkDto
{
    public string Content { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}
