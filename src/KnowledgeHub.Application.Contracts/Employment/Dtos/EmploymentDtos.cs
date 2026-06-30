using System;
using System.Collections.Generic;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Employment.Dtos;

public class EmployerProfileDto
{
    public Guid UserId { get; set; }
    public string? ContactName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public string? UnifiedSocialCreditCode { get; set; }
    public string? Position { get; set; }
    public string? Industry { get; set; }
    public string? PartnerSchool { get; set; }
    public string? Remark { get; set; }
}

public class UpdateEmployerProfileDto
{
    public string? ContactName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string UnifiedSocialCreditCode { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? PartnerSchool { get; set; }
    public string? Remark { get; set; }
}

public class JobPostingDto : FullAuditedEntityDto<Guid>
{
    public Guid EmployerUserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Address { get; set; }
    public EmploymentJobType JobType { get; set; }
    public string? EducationRequirement { get; set; }
    public string? SalaryRange { get; set; }
    public int RecruitmentCount { get; set; }
    public string? SkillTags { get; set; }
    public string? Benefits { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? Deadline { get; set; }
    public EmploymentJobStatus Status { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public int ApplicationCount { get; set; }
    public bool HasApplied { get; set; }
}

public class CreateUpdateJobPostingDto
{
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Address { get; set; }
    public EmploymentJobType JobType { get; set; }
    public string? EducationRequirement { get; set; }
    public string? SalaryRange { get; set; }
    public int RecruitmentCount { get; set; } = 1;
    public string? SkillTags { get; set; }
    public string? Benefits { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? Deadline { get; set; }
    public EmploymentJobStatus Status { get; set; }
}

public class ReviewJobPostingDto
{
    public EmploymentJobStatus Status { get; set; }
    public string? ReviewComment { get; set; }
}

public class PagedJobPostingRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Location { get; set; }
    public EmploymentJobType? JobType { get; set; }
}

public class GetManageJobsInput : PagedJobPostingRequestDto
{
    public EmploymentJobStatus? Status { get; set; }
}

public class StudentResumeDto : FullAuditedEntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? SchoolName { get; set; }
    public string? Major { get; set; }
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public string? StudentNumber { get; set; }
    public string? Summary { get; set; }
    public string? Skills { get; set; }
    public string? EducationExperience { get; set; }
    public string? InternshipExperience { get; set; }
    public string? ProjectExperience { get; set; }
    public string? CertificateText { get; set; }
    public string? AttachmentUrl { get; set; }
    public bool IsDefault { get; set; }
    public int VersionNo { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class CreateUpdateStudentResumeDto
{
    public string Title { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? SchoolName { get; set; }
    public string? Major { get; set; }
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public string? StudentNumber { get; set; }
    public string? Summary { get; set; }
    public string? Skills { get; set; }
    public string? EducationExperience { get; set; }
    public string? InternshipExperience { get; set; }
    public string? ProjectExperience { get; set; }
    public string? CertificateText { get; set; }
    public string? AttachmentUrl { get; set; }
    public bool IsDefault { get; set; }
}

public class JobApplicationDto : FullAuditedEntityDto<Guid>
{
    public Guid JobPostingId { get; set; }
    public string? JobTitle { get; set; }
    public string? CompanyName { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid ResumeId { get; set; }
    public string? ResumeTitle { get; set; }
    public string? CoverLetter { get; set; }
    public EmploymentApplicationStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? EmployerRemark { get; set; }
}

public class CreateJobApplicationDto
{
    public Guid JobPostingId { get; set; }
    public Guid ResumeId { get; set; }
    public string? CoverLetter { get; set; }
}

public class GetJobApplicationsInput : PagedAndSortedResultRequestDto
{
    public Guid? JobPostingId { get; set; }
    public Guid? StudentId { get; set; }
    public EmploymentApplicationStatus? Status { get; set; }
}

public class UpdateJobApplicationStatusDto
{
    public EmploymentApplicationStatus Status { get; set; }
    public string? EmployerRemark { get; set; }
}

public class InterviewScheduleDto : FullAuditedEntityDto<Guid>
{
    public Guid ApplicationId { get; set; }
    public Guid JobPostingId { get; set; }
    public string? JobTitle { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid? EmployerUserId { get; set; }
    /// <summary>P1-8：面试官用户 ID（与 InterviewerName 二选一）</summary>
    public Guid? InterviewerId { get; set; }
    public string InterviewerName { get; set; } = string.Empty;
    public string? InterviewerPhone { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Note { get; set; }
    public EmploymentInterviewResult Result { get; set; }
    public string? Summary { get; set; }
    public string? ResultComment { get; set; }
    public DateTime? ResultRecordedAt { get; set; }
}

public class CreateUpdateInterviewScheduleDto
{
    public Guid ApplicationId { get; set; }
    /// <summary>
    /// 面试官用户 ID（P1-8）。可选：未选择则仅保留 InterviewerName 自由文本。
    /// </summary>
    public Guid? InterviewerId { get; set; }
    public string InterviewerName { get; set; } = string.Empty;
    public string? InterviewerPhone { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Note { get; set; }
}

public class RecordInterviewResultDto
{
    public EmploymentInterviewResult Result { get; set; }
    public string? Summary { get; set; }
    public string? ResultComment { get; set; }
}

public class GetInterviewSchedulesInput : PagedAndSortedResultRequestDto
{
    public Guid? JobPostingId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? ApplicationId { get; set; }
    public EmploymentInterviewResult? Result { get; set; }
}

public class EmploymentGuidanceRecordDto : FullAuditedEntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid? ApplicationId { get; set; }
    /// <summary>教师 ID；AI 生成的指导记录为 null。</summary>
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public EmploymentGuidanceSourceType SourceType { get; set; }
    public string? CareerGoal { get; set; }
    public DateTime GuidedAt { get; set; }
}

public class CreateEmploymentGuidanceRecordDto
{
    public Guid StudentId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public EmploymentGuidanceSourceType SourceType { get; set; }
    public string? CareerGoal { get; set; }
}

/// <summary>
/// 学生端保存自己 AI 生成的就业指导。
/// StudentId 强制绑定当前用户，SourceType 固定为 AI，TeacherId 固定为 null。
/// </summary>
public class CreateMyAIGuidanceRecordDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CareerGoal { get; set; }
}

public class GetEmploymentGuidanceRecordsInput : PagedAndSortedResultRequestDto
{
    public Guid? StudentId { get; set; }
    public Guid? ApplicationId { get; set; }
}

public class EmploymentOutcomeDto : FullAuditedEntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid? ApplicationId { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public EmploymentOutcomeStatus Status { get; set; }
    public string? EmploymentType { get; set; }
    public string? Region { get; set; }
    public string? SalaryRange { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public string? Remark { get; set; }
    public bool IsPrimary { get; set; }
}

public class CreateUpdateEmploymentOutcomeDto
{
    public Guid? Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public EmploymentOutcomeStatus Status { get; set; }
    public string? EmploymentType { get; set; }
    public string? Region { get; set; }
    public string? SalaryRange { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public string? Remark { get; set; }
    public bool IsPrimary { get; set; } = true;
}

public class GetEmploymentOutcomeListInput : PagedAndSortedResultRequestDto
{
    public Guid? StudentId { get; set; }
    public EmploymentOutcomeStatus? Status { get; set; }
    public bool? OnlyPrimary { get; set; }
}

public class EmploymentStatisticsInput
{
    public string? Major { get; set; }
    public string? Grade { get; set; }
    public EmploymentOutcomeStatus? Status { get; set; }
}

public class EmploymentStatisticsRowDto
{
    public string Major { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public EmploymentOutcomeStatus Status { get; set; }
    public int StudentCount { get; set; }
    public int OutcomeCount { get; set; }
}

/// <summary>
/// 面试官候选简要信息（用于下拉列表）
/// </summary>
public class InterviewerCandidateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
