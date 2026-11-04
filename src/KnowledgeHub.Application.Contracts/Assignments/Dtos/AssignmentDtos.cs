using System;
using System.Collections.Generic;
using KnowledgeHub.Assignments.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Assignments.Dtos;

// ══════════════════════════════════════════════════════════
//  公共
// ══════════════════════════════════════════════════════════

/// <summary>作业文件信息。</summary>
public class AssignmentFileDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public AssignmentFileType Type { get; set; }
    public int Version { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>评分细则项。</summary>
public class AssignmentCriterionDto
{
    /// <summary>细则 Id（新建时为空）。</summary>
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}

// ══════════════════════════════════════════════════════════
//  教师端
// ══════════════════════════════════════════════════════════

/// <summary>作业列表查询条件。</summary>
public class AssignmentListQueryDto : PagedAndSortedResultRequestDto
{
    public Guid? CourseId { get; set; }
    public AssignmentStatus? Status { get; set; }
    public string? Filter { get; set; }
}

/// <summary>作业列表项（教师端）。</summary>
public class AssignmentListItemDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssignmentType Type { get; set; }
    public AssignmentStatus Status { get; set; }
    public decimal MaxScore { get; set; }
    public DateTime Deadline { get; set; }
    public bool AllowLate { get; set; }
    public decimal LatePenaltyPercent { get; set; }
    public int TotalStudents { get; set; }
    public int SubmittedCount { get; set; }
    public int GradedCount { get; set; }
    public int PendingGradeCount { get; set; }
    public int NotSubmittedCount { get; set; }
    public int LateCount { get; set; }
    public decimal AverageScore { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? PublishTime { get; set; }
}

/// <summary>作业统计卡片（教师端）。</summary>
public class AssignmentStatisticsDto
{
    public int TotalCount { get; set; }
    /// <summary>进行中（已发布且未结束）</summary>
    public int OngoingCount { get; set; }
    /// <summary>待批阅份数</summary>
    public int PendingGradeCount { get; set; }
    /// <summary>已批阅平均分</summary>
    public decimal AverageScore { get; set; }
    /// <summary>迟交人次</summary>
    public int LateCount { get; set; }
    /// <summary>课程学生数</summary>
    public int TotalStudents { get; set; }
}

/// <summary>作业详情（教师端）。</summary>
public class AssignmentDetailDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public Guid? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssignmentType Type { get; set; }
    public AssignmentStatus Status { get; set; }
    public decimal MaxScore { get; set; }
    public DateTime Deadline { get; set; }
    public bool AllowLate { get; set; }
    public decimal LatePenaltyPercent { get; set; }
    public string AllowedFileTypes { get; set; } = string.Empty;
    public int MaxFileSizeMB { get; set; }
    public int MaxFileCount { get; set; }
    public bool AllowResubmit { get; set; }
    public Guid TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? PublishTime { get; set; }
    public DateTime? ClosedTime { get; set; }

    public List<AssignmentCriterionDto> Criteria { get; set; } = new();
    public List<AssignmentFileDto> Attachments { get; set; } = new();

    public int TotalStudents { get; set; }
    public int SubmittedCount { get; set; }
    public int GradedCount { get; set; }
    public int PendingGradeCount { get; set; }
    public int NotSubmittedCount { get; set; }
    public int LateCount { get; set; }
    public decimal AverageScore { get; set; }
}

/// <summary>新建 / 编辑作业。</summary>
public class CreateUpdateAssignmentDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssignmentType Type { get; set; } = AssignmentType.Individual;
    public decimal MaxScore { get; set; } = 100m;
    public DateTime Deadline { get; set; }
    public bool AllowLate { get; set; } = true;
    public decimal LatePenaltyPercent { get; set; }
    public string AllowedFileTypes { get; set; } = "pdf,doc,docx,xls,xlsx,ppt,pptx,zip,rar,7z,png,jpg,jpeg,txt,md";
    public int MaxFileSizeMB { get; set; } = 20;
    public int MaxFileCount { get; set; } = 5;
    public bool AllowResubmit { get; set; } = true;
    /// <summary>评分细则（按顺序）</summary>
    public List<AssignmentCriterionDto> Criteria { get; set; } = new();
    /// <summary>已上传的「作业要求附件」文件 Id</summary>
    public List<Guid> AttachmentFileIds { get; set; } = new();
}

/// <summary>学生提交列表查询条件（教师端批阅用）。</summary>
public class AssignmentSubmissionQueryDto : PagedAndSortedResultRequestDto
{
    public Guid AssignmentId { get; set; }
    public SubmissionStatus? Status { get; set; }
    /// <summary>只看未提交的（缺交名单）</summary>
    public bool? OnlyNotSubmitted { get; set; }
    public string? Filter { get; set; }
}

/// <summary>学生提交列表项（教师端）。</summary>
public class AssignmentSubmissionItemDto
{
    /// <summary>提交 Id；未提交时为 Guid.Empty</summary>
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public string AssignmentTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public SubmissionStatus Status { get; set; }
    /// <summary>是否已提交（含草稿）</summary>
    public bool Submitted { get; set; }
    public bool IsLate { get; set; }
    public decimal? Score { get; set; }
    public decimal MaxScore { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int Version { get; set; }
    public int FileCount { get; set; }
}

/// <summary>提交详情（教师端批阅）。</summary>
public class AssignmentSubmissionDetailDto
{
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public string AssignmentTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public SubmissionStatus Status { get; set; }
    public decimal MaxScore { get; set; }
    public decimal? Score { get; set; }
    public string? Note { get; set; }
    public bool HonorPledge { get; set; }
    public bool IsLate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerName { get; set; }
    public string? Comment { get; set; }
    public bool AllowResubmit { get; set; }
    public int Version { get; set; }

    public List<AssignmentFileDto> Files { get; set; } = new();
    public List<AssignmentFileDto> AnnotatedFiles { get; set; } = new();
    public List<SubmissionScoreDto> Scores { get; set; } = new();
    public List<AssignmentCriterionDto> Criteria { get; set; } = new();
}

/// <summary>得分明细项。</summary>
public class SubmissionScoreDto
{
    public Guid Id { get; set; }
    public Guid? CriterionId { get; set; }
    public string CriterionName { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>批阅输入。</summary>
public class GradeAssignmentSubmissionDto
{
    /// <summary>总分（不传则按明细求和）</summary>
    public decimal? Score { get; set; }

    /// <summary>教师评语</summary>
    public string? Comment { get; set; }

    /// <summary>是否允许学生重新提交</summary>
    public bool AllowResubmit { get; set; }

    /// <summary>是否按迟交规则自动扣分</summary>
    public bool ApplyLatePenalty { get; set; }

    /// <summary>得分明细</summary>
    public List<SubmissionScoreInputDto> Scores { get; set; } = new();
}

/// <summary>得分明细输入。</summary>
public class SubmissionScoreInputDto
{
    public Guid? CriterionId { get; set; }
    public string? CriterionName { get; set; }
    public decimal? Score { get; set; }
    public string? Comment { get; set; }
    public int SortOrder { get; set; }
}

// ══════════════════════════════════════════════════════════
//  学生端
// ══════════════════════════════════════════════════════════

/// <summary>学生端作业列表项。</summary>
public class StudentAssignmentItemDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public decimal MaxScore { get; set; }
    public DateTime Deadline { get; set; }
    public SubmissionStatus Status { get; set; }
    public bool Submitted { get; set; }
    public bool IsLate { get; set; }
    public decimal? Score { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool HasFeedback { get; set; }
    /// <summary>剩余天数（已过为 0）</summary>
    public int RemainingDays { get; set; }
}

/// <summary>学生端作业详情（含作业要求 + 我的提交）。</summary>
public class StudentAssignmentDetailDto
{
    public Guid AssignmentId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssignmentType Type { get; set; }
    public decimal MaxScore { get; set; }
    public DateTime Deadline { get; set; }
    public bool AllowLate { get; set; }
    public decimal LatePenaltyPercent { get; set; }
    public string AllowedFileTypes { get; set; } = string.Empty;
    public int MaxFileSizeMB { get; set; }
    public int MaxFileCount { get; set; }
    public bool AllowResubmit { get; set; }
    public string? TeacherName { get; set; }
    public int RemainingDays { get; set; }

    public List<AssignmentCriterionDto> Criteria { get; set; } = new();
    public List<AssignmentFileDto> Attachments { get; set; } = new();

    // ── 我的提交 ──
    public Guid? SubmissionId { get; set; }
    public SubmissionStatus MyStatus { get; set; }
    public bool Submitted { get; set; }
    public bool IsLate { get; set; }
    public string? MyNote { get; set; }
    public bool HonorPledge { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int Version { get; set; }
    public List<AssignmentFileDto> MyFiles { get; set; } = new();
    /// <summary>当前是否允许提交（未超期 / 允许迟交 / 教师允许重交）</summary>
    public bool CanSubmit { get; set; }
}

/// <summary>学生端提交 / 保存草稿输入。</summary>
public class StudentSubmitAssignmentDto
{
    public Guid AssignmentId { get; set; }
    /// <summary>补充说明</summary>
    public string? Note { get; set; }
    /// <summary>学术诚信承诺</summary>
    public bool HonorPledge { get; set; }
    /// <summary>true = 保存草稿，false = 正式提交</summary>
    public bool IsDraft { get; set; }
    /// <summary>已上传文件 Id</summary>
    public List<Guid> FileIds { get; set; } = new();
}

/// <summary>学生端成绩与反馈。</summary>
public class StudentAssignmentFeedbackDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal? Score { get; set; }
    /// <summary>班级平均分</summary>
    public decimal ClassAverage { get; set; }
    public bool Graded { get; set; }
    public string? Comment { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsLate { get; set; }

    public List<SubmissionScoreDto> Scores { get; set; } = new();
    public List<AssignmentFileDto> Files { get; set; } = new();
    public List<AssignmentFileDto> AnnotatedFiles { get; set; } = new();
    public List<StudentSubmissionTimelineDto> Timeline { get; set; } = new();
}

/// <summary>提交 / 批阅时间线项。</summary>
public class StudentSubmissionTimelineDto
{
    public string Label { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public string? Extra { get; set; }
}
