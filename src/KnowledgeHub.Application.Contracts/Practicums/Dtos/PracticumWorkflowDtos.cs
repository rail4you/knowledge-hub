using System;
using System.Collections.Generic;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Practicums.Dtos;

public class PracticumEnrollmentDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public PracticumEnrollmentStatus Status { get; set; }
    public decimal Progress { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastSubmittedAt { get; set; }
    public decimal? FinalScore { get; set; }
    public string? FinalComment { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class GetPracticumEnrollmentsInput : PagedAndSortedResultRequestDto
{
    public Guid? ProjectId { get; set; }
    public Guid? StudentId { get; set; }
    public PracticumEnrollmentStatus? Status { get; set; }
}

public class PracticumSubmissionDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public Guid TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public int VersionNo { get; set; }
    public string? Content { get; set; }
    public string? AttachmentUrls { get; set; }
    public string? LinkUrl { get; set; }
    public string? ScreenshotUrls { get; set; }
    public PracticumSubmissionStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? TeacherFeedback { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public decimal? Score { get; set; }
}

public class CreatePracticumSubmissionDto
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public string? Content { get; set; }
    public string? AttachmentUrls { get; set; }
    public string? LinkUrl { get; set; }
    public string? ScreenshotUrls { get; set; }
}

public class GetPracticumSubmissionsInput : PagedAndSortedResultRequestDto
{
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? EnrollmentId { get; set; }
    public Guid? StudentId { get; set; }
    public PracticumSubmissionStatus? Status { get; set; }
}

public class PracticumGuidanceRecordDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public Guid TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsVisibleToStudent { get; set; }
    public DateTime GuidedAt { get; set; }
}

public class CreatePracticumGuidanceRecordDto
{
    public Guid EnrollmentId { get; set; }
    public Guid? TaskId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsVisibleToStudent { get; set; } = true;
}

public class PracticumAssessmentDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public decimal Score { get; set; }
    public string? GradeLevel { get; set; }
    public string? Comment { get; set; }
    public string? RubricJson { get; set; }
    public DateTime AssessedAt { get; set; }
}

public class CreatePracticumAssessmentDto
{
    public Guid? SubmissionId { get; set; }
    public decimal Score { get; set; }
    public string? GradeLevel { get; set; }
    public string? Comment { get; set; }
    public string? RubricJson { get; set; }
}

public class PracticumTimelineItemDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? OperatorName { get; set; }
    public DateTime Time { get; set; }
    public Dictionary<string, string?> Metadata { get; set; } = new();
}
