using System;
using System.Collections.Generic;
using KnowledgeHub.Classroom.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Classroom.Dtos;

// ══════════════════════════════════════════════════════════════════
//  讨论答疑
// ══════════════════════════════════════════════════════════════════

public class DiscussionDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public bool IsPinned { get; set; }
    public bool IsResolved { get; set; }
    public int ReplyCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime? LastReplyAt { get; set; }

    /// <summary>是否是当前用户发的（前端据此显示删除按钮）。</summary>
    public bool IsMine { get; set; }

    /// <summary>详情接口才返回回复列表。</summary>
    public List<DiscussionReplyDto> Replies { get; set; } = new();
}

public class DiscussionReplyDto : FullAuditedEntityDto<Guid>
{
    public Guid DiscussionId { get; set; }
    public Guid? ParentReplyId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public bool IsAccepted { get; set; }
}

public class GetDiscussionListInput
{
    public Guid? CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    /// <summary>只看未解决。</summary>
    public bool OnlyUnresolved { get; set; }
    /// <summary>只看我发的。</summary>
    public bool OnlyMine { get; set; }
    public string? Filter { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 20;
}

public class CreateDiscussionInput
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CreateDiscussionReplyInput
{
    public Guid DiscussionId { get; set; }
    public Guid? ParentReplyId { get; set; }
    public string Content { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════════════
//  签到
// ══════════════════════════════════════════════════════════════════

public class CheckInSessionDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Token { get; set; } = string.Empty;
    public CheckInSessionStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool RequireLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? RadiusMeters { get; set; }

    /// <summary>已签到人数。</summary>
    public int CheckedInCount { get; set; }

    /// <summary>应签到人数（选课人数，不含已退课）。</summary>
    public int ExpectedCount { get; set; }

    /// <summary>当前用户是否已签到（学生端用）。</summary>
    public bool ICheckedIn { get; set; }

    /// <summary>二维码短链（相对路径，前端拼 origin）。</summary>
    public string ShortUrl => "/check-in/" + Token;
}

public class CreateCheckInSessionInput
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>有效分钟数；为空表示手动关闭。</summary>
    public int? DurationMinutes { get; set; }
    public bool RequireLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? RadiusMeters { get; set; }
}

public class CheckInRecordDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentUserName { get; set; }
    public DateTime CheckedInAt { get; set; }
    public string? Location { get; set; }
}

/// <summary>签到名单：已签到 + 未签到（教师端导出/催促用）。</summary>
public class CheckInRosterDto
{
    public Guid SessionId { get; set; }
    public List<CheckInRecordDto> CheckedIn { get; set; } = new();
    public List<CheckInAbsentDto> Absent { get; set; } = new();
}

public class CheckInAbsentDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentUserName { get; set; }
}

public class DoCheckInInput
{
    public string Token { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class DoCheckInResultDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CheckedInAt { get; set; }
    /// <summary>是否本次新增（false 表示之前已签过，幂等返回）。</summary>
    public bool IsNew { get; set; }
}

public class MyCheckInRecordDto
{
    public Guid SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public DateTime CheckedInAt { get; set; }
    public DateTime StartTime { get; set; }
}

// ══════════════════════════════════════════════════════════════════
//  课堂练习
// ══════════════════════════════════════════════════════════════════

public class PracticeSessionDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public ClassroomPracticeStatus Status { get; set; }
    public Guid? ChapterId { get; set; }
    public string? KnowledgeResourceIds { get; set; }
    public int QuestionCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>已提交人数。</summary>
    public int SubmittedCount { get; set; }

    /// <summary>应参与人数（选课人数）。</summary>
    public int ExpectedCount { get; set; }

    /// <summary>提交率（0~1）。</summary>
    public double SubmitRate { get; set; }

    public string ShortUrl => "/classroom-practice/" + Token;
}

public class CreatePracticeSessionInput
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? ChapterId { get; set; }
    public List<Guid>? KnowledgeResourceIds { get; set; }
    /// <summary>抽题数量（默认 5）。</summary>
    public int QuestionCount { get; set; } = 5;
}

/// <summary>学生取题（不含答案）。</summary>
public class PracticePaperDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ClassroomPracticeStatus Status { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public List<PracticePaperItemDto> Items { get; set; } = new();

    /// <summary>已提交过的话，返回上次结果（用于"看一下我刚交的"）。</summary>
    public PracticeAttemptResultDto? MyAttempt { get; set; }
}

public class PracticePaperItemDto
{
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? Options { get; set; }
}

public class SubmitPracticeInput
{
    public string Token { get; set; } = string.Empty;
    public List<SubmitPracticeAnswerInput> Answers { get; set; } = new();
}

public class SubmitPracticeAnswerInput
{
    public Guid ExerciseId { get; set; }
    public string? Answer { get; set; }
}

public class PracticeAttemptResultDto
{
    public Guid AttemptId { get; set; }
    public Guid SessionId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public double Accuracy { get; set; }
    public List<PracticeAnswerResultDto> Answers { get; set; } = new();
}

public class PracticeAnswerResultDto
{
    public Guid ExerciseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string? CorrectAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public string? AnswerExplanation { get; set; }
}

/// <summary>教师端实时统计。</summary>
public class PracticeStatsDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SubmittedCount { get; set; }
    public int ExpectedCount { get; set; }
    public double SubmitRate { get; set; }
    public double AverageAccuracy { get; set; }
    public List<PracticeItemStatsDto> Items { get; set; } = new();
    public List<PracticeSubmitterDto> Submitters { get; set; } = new();
    public List<CheckInAbsentDto> NotSubmitted { get; set; } = new();
}

public class PracticeItemStatsDto
{
    public Guid ExerciseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }
    public double CorrectRate { get; set; }
}

public class PracticeSubmitterDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
}

// ══════════════════════════════════════════════════════════════════
//  小组任务
// ══════════════════════════════════════════════════════════════════

public class GroupTaskDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Token { get; set; } = string.Empty;
    public GroupTaskStatus Status { get; set; }
    public GroupMode GroupMode { get; set; }
    public DateTime? PublishAt { get; set; }
    public DateTime? DueAt { get; set; }
    public int MaxScore { get; set; }

    /// <summary>组数。</summary>
    public int GroupCount { get; set; }

    /// <summary>已提交组数。</summary>
    public int SubmittedGroupCount { get; set; }

    public int StudentCount { get; set; }

    public string ShortUrl => "/group-task/" + Token;
}

public class GroupTaskDetailDto : GroupTaskDto
{
    public List<StudentGroupDto> Groups { get; set; } = new();
}

public class StudentGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
    public GroupTaskSubmissionDto? Submission { get; set; }
}

public class GroupMemberDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentUserName { get; set; }
}

public class GroupTaskSubmissionDto
{
    public Guid Id { get; set; }
    public Guid GroupTaskId { get; set; }
    public Guid GroupId { get; set; }
    public string? GroupName { get; set; }
    public string? Content { get; set; }
    public Guid? AttachmentResourceId { get; set; }
    public string? AttachmentFileName { get; set; }
    public Guid SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAt { get; set; }
}

public class CreateGroupTaskInput
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public int MaxScore { get; set; } = 100;
    public GroupMode GroupMode { get; set; } = GroupMode.RandomAuto;
    /// <summary>随机分组时的组数；为空按每组 4 人估算。</summary>
    public int? GroupCount { get; set; }
    /// <summary>创建后立即发布。</summary>
    public bool PublishImmediately { get; set; }
}

public class AssignGroupsInput
{
    public Guid GroupTaskId { get; set; }
    /// <summary>手工分组（GroupMode=TeacherAssigned 时使用）。</summary>
    public List<AssignGroupItemInput> Groups { get; set; } = new();
}

public class AssignGroupItemInput
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> StudentIds { get; set; } = new();
}

public class SubmitGroupTaskInput
{
    public string Token { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Guid? AttachmentResourceId { get; set; }
}

public class GradeGroupTaskInput
{
    public Guid SubmissionId { get; set; }
    public int Score { get; set; }
    public string? Feedback { get; set; }
}

// ══════════════════════════════════════════════════════════════════
//  免登录 token 解析
// ══════════════════════════════════════════════════════════════════

public class ResolveClassroomTokenInput
{
    public string Token { get; set; } = string.Empty;
    public ClassroomTokenKind Kind { get; set; }
}

/// <summary>
/// 短链页面拿到的会话元信息。
/// 刻意**不含**任何名单/成绩，避免 token 泄露导致批量枚举个人信息。
/// </summary>
public class ClassroomTokenSessionDto
{
    public ClassroomTokenKind Kind { get; set; }
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>状态文本（进行中 / 已结束 / 已发布 / 草稿）。</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>是否还接受提交/签到。</summary>
    public bool IsOpen { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? DueAt { get; set; }
    public int? QuestionCount { get; set; }
    public int MaxScore { get; set; }

    /// <summary>当前登录用户是否已参与（签到过 / 已提交）。</summary>
    public bool Participated { get; set; }

    /// <summary>小组任务：当前用户所在组名（非本组成员时为 null）。</summary>
    public string? MyGroupName { get; set; }
}
