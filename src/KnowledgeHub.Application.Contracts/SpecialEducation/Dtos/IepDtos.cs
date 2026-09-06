using System;
using System.Collections.Generic;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.SpecialEducation.Dtos;

public class IepPlanDto : FullAuditedEntityDto<Guid>
{
    public Guid StudentUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string ProfileSummary { get; set; } = string.Empty;
    public List<string> LongTermGoals { get; set; } = new();
    public List<string> ShortTermGoals { get; set; } = new();
    public List<string> Strategies { get; set; } = new();
    public List<string> Evaluation { get; set; } = new();
    public List<string> HomeSchool { get; set; } = new();
    public string LegalBasis { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public Guid? ParentVersionId { get; set; }
    public SpecialEduPlanStatus Status { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerName { get; set; }
}

public class GenerateIepInputDto
{
    public Guid StudentUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public Guid? CourseId { get; set; }
    /// <summary>手动表单：评估数据/障碍类型/发展水平（JSON 或自由文本）。</summary>
    public string AssessmentData { get; set; } = string.Empty;
    public string CurrentLevel { get; set; } = string.Empty;
    public string FamilyNeeds { get; set; } = string.Empty;
    public string? CustomPrompt { get; set; }
}

public class SaveIepInputDto
{
    public Guid? Id { get; set; }
    public Guid StudentUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public Guid? CourseId { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public string SourceInputJson { get; set; } = "{}";
}

public class GetIepListInputDto : PagedAndSortedResultRequestDto
{
    public SpecialEduCategory? Category { get; set; }
    public SpecialEduPlanStatus? Status { get; set; }
    public Guid? StudentUserId { get; set; }
    public string? Keyword { get; set; }
}

public class ReviewIepInputDto
{
    public Guid Id { get; set; }
    public bool Approved { get; set; }
    public string? Comment { get; set; }
}

public class SubmitIepForReviewInputDto
{
    public Guid Id { get; set; }
    /// <summary>指派的审核教师（租户内教师）。为空则仅提交待审核。</summary>
    public Guid? ReviewerUserId { get; set; }
}

public class ExportIepInputDto
{
    public string ResultJson { get; set; } = string.Empty;
}
