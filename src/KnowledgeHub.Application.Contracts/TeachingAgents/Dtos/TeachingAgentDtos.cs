using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.TeachingAgents.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.TeachingAgents.Dtos;

public class TeachingAgentSkillBindingDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class TeachingAgentPresetDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string WelcomeMessage { get; set; } = string.Empty;
    public string SuggestedTaskPrompt { get; set; } = string.Empty;
    public List<TeachingAgentSkillBindingDto> Skills { get; set; } = new();
}

public class TeachingAgentVersionDto : FullAuditedEntityDto<Guid>
{
    public Guid TeachingAgentId { get; set; }
    public int VersionNumber { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string? WelcomeMessage { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public List<TeachingAgentSkillBindingDto> Skills { get; set; } = new();
    public string? VersionNote { get; set; }
    public bool IsPublished { get; set; }
}

public class TeachingAgentDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public string OwnerUserName { get; set; } = string.Empty;
    public TeachingAgentVisibility Visibility { get; set; }
    public TeachingAgentStatus Status { get; set; }
    public Guid? PublishedVersionId { get; set; }
    public TeachingAgentVersionDto? DraftVersion { get; set; }
}

public class TeachingAgentDetailDto : TeachingAgentDto
{
    public TeachingAgentVersionDto? PublishedVersion { get; set; }
    public List<TeachingAgentVersionDto> Versions { get; set; } = new();
}

public class CreateUpdateTeachingAgentDto
{
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TeachingAgentVisibility Visibility { get; set; } = TeachingAgentVisibility.Private;

    [MaxLength(20000)]
    public string SystemPrompt { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? WelcomeMessage { get; set; }

    public string ModelId { get; set; } = "qwen-flash";
    public double Temperature { get; set; } = 0.2;

    [MaxLength(500)]
    public string? VersionNote { get; set; }

    public List<TeachingAgentSkillBindingDto> Skills { get; set; } = new();
}

public class CloneTeachingAgentFromPresetDto
{
    public string PresetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TeachingAgentVisibility Visibility { get; set; } = TeachingAgentVisibility.Private;
}

public class PublishTeachingAgentVersionDto
{
    public string? VersionNote { get; set; }
}

public class PagedTeachingAgentRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public class TeachingAgentOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
    public int VersionNumber { get; set; }
}

public class StudentOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    /// <summary>所属教学班 Id（未分班时为空）。</summary>
    public Guid? ClassId { get; set; }

    /// <summary>所属教学班名称（未分班时为空）。</summary>
    public string? ClassName { get; set; }

    /// <summary>专业 Id：优先取教学班的专业，其次取用户档案上的专业。</summary>
    public Guid? MajorId { get; set; }

    /// <summary>专业名称。</summary>
    public string? MajorName { get; set; }
}

public class CourseOptionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>该课程含有的习题数量（用于判断是否可作为「习题集」任务对象）。</summary>
    public int ExerciseCount { get; set; }
}

public class ResourceOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
}

public class ExerciseOptionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Difficulty { get; set; }
    public int Score { get; set; }
}

public class TaskCreationOptionsDto
{
    public List<TeachingAgentOptionDto> Agents { get; set; } = new();
    public List<StudentOptionDto> Students { get; set; } = new();
    public List<CourseOptionDto> Courses { get; set; } = new();
    public List<ResourceOptionDto> Resources { get; set; } = new();
}

public class TeachingAgentCourseChapterDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class TeachingAgentKnowledgeResourceDto
{
    public Guid Id { get; set; }
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public int Difficulty { get; set; }
}

public class TeachingAgentCourseContextDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TeacherName { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int Difficulty { get; set; }
    public List<TeachingAgentCourseChapterDto> Chapters { get; set; } = new();
    public List<TeachingAgentKnowledgeResourceDto> KnowledgeResources { get; set; } = new();
}

public class TeachingAgentResourceContextDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public string? CategoryName { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
}

public class TeachingAgentExerciseContextDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public int Difficulty { get; set; }
    public int Score { get; set; }
    public Guid? ChapterId { get; set; }
    public string? QuestionAnalysis { get; set; }
}

public class TaskTargetSnapshotDto
{
    public ClassroomAgentTaskTargetType TargetType { get; set; }
    public TeachingAgentCourseContextDto? Course { get; set; }
    public TeachingAgentResourceContextDto? Resource { get; set; }
    public List<TeachingAgentExerciseContextDto> Exercises { get; set; } = new();
}

public class CreateClassroomAgentTaskDto
{
    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid TeachingAgentVersionId { get; set; }

    [MaxLength(4000)]
    public string TaskPrompt { get; set; } = string.Empty;
    public ClassroomAgentTaskTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    /// <summary>习题集任务选定的习题（TargetType=ExerciseSet 时使用；此时 TargetId 为该习题所属课程）。</summary>
    public List<Guid> ExerciseIds { get; set; } = new();
    public DateTime? DueTime { get; set; }
    public List<Guid> StudentIds { get; set; } = new();
}

public class ClassroomAgentAssignmentDto : FullAuditedEntityDto<Guid>
{
    public Guid ClassroomAgentTaskId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public ClassroomAgentAssignmentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public string? SubmissionSummary { get; set; }
    public string? HelpReason { get; set; }
    public string? TeacherResponse { get; set; }
}

public class ClassroomAgentTaskDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TeachingAgentId { get; set; }
    public Guid TeachingAgentVersionId { get; set; }
    public string TeachingAgentName { get; set; } = string.Empty;
    public int TeachingAgentVersionNumber { get; set; }
    public string? WelcomeMessage { get; set; }
    public string TaskPrompt { get; set; } = string.Empty;
    public ClassroomAgentTaskTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public TaskTargetSnapshotDto? TargetSnapshot { get; set; }
    public DateTime? DueTime { get; set; }
    public ClassroomAgentTaskPublishStatus PublishStatus { get; set; }
    public int AssignmentCount { get; set; }
    public int CompletedCount { get; set; }
    public int NeedsHelpCount { get; set; }
}

public class ClassroomAgentTaskDetailDto : ClassroomAgentTaskDto
{
    public List<ClassroomAgentAssignmentDto> Assignments { get; set; } = new();
}

public class StudentAgentTaskDto
{
    public Guid AssignmentId { get; set; }
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TeachingAgentName { get; set; } = string.Empty;
    public ClassroomAgentAssignmentStatus Status { get; set; }
    public DateTime? DueTime { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

public class AgentRunMessageDto : FullAuditedEntityDto<Guid>
{
    public Guid AgentRunId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ToolCallsJson { get; set; } = "[]";
}

public class AgentRunDto : FullAuditedEntityDto<Guid>
{
    public Guid ClassroomAgentAssignmentId { get; set; }
    public string ThreadId { get; set; } = string.Empty;
    public AgentRunStatus RuntimeStatus { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? LastError { get; set; }
}

public class AgentRunDetailDto
{
    public ClassroomAgentTaskDetailDto Task { get; set; } = new();
    public ClassroomAgentAssignmentDto Assignment { get; set; } = new();
    public AgentRunDto Run { get; set; } = new();
    public List<AgentRunMessageDto> Messages { get; set; } = new();
}

public class SubmitAgentAssignmentDto
{
    [MaxLength(20000)]
    public string Summary { get; set; } = string.Empty;
}

public class NeedTeacherHelpDto
{
    [MaxLength(4000)]
    public string Reason { get; set; } = string.Empty;
}

public class TeacherRespondDto
{
    [MaxLength(20000)]
    public string Response { get; set; } = string.Empty;
}

public class SendAgentRunMessageDto
{
    [MaxLength(20000)]
    public string Message { get; set; } = string.Empty;
}

public class AgentMessageChunkDto
{
    public string Content { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}

// ══════════════════════════════════════════════════════════
//  互动课堂课表（基于「课堂智能体任务」）
// ══════════════════════════════════════════════════════════

/// <summary>
/// 课表查询条件。
///
/// <para>课表数据源 = 课堂智能体任务：老师给智能体分配任务时设定截止时间，
/// 课表把任务按「布置日 / 截止日」排到日/周/月视图里，
/// 鼠标悬浮显示任务名称、日期、时间段、学校。</para>
/// </summary>
public class ClassroomTimetableQueryDto
{
    /// <summary>区间起点（本地时间，含）。</summary>
    public DateTime From { get; set; }

    /// <summary>区间终点（本地时间，含）。</summary>
    public DateTime To { get; set; }

    /// <summary>按课程筛选（可选）。</summary>
    public Guid? CourseId { get; set; }

    /// <summary>只显示已发布的任务（默认 true）。</summary>
    public bool PublishedOnly { get; set; } = true;
}

/// <summary>课表条目（一条 = 一个智能体任务在课表上的某个时间点）。</summary>
public class ClassroomTimetableItemDto
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>课程名称（来自任务目标快照）。</summary>
    public string? CourseName { get; set; }
    public Guid? CourseId { get; set; }

    /// <summary>智能体名称。</summary>
    public string? AgentName { get; set; }

    /// <summary>任务目标类型：0 课程 / 1 资源 / 2 习题集。</summary>
    public int TargetType { get; set; }

    /// <summary>课表类型：assign（布置日）/ due（截止时间）。</summary>
    public string Kind { get; set; } = "due";

    /// <summary>该条目在课表上的时间。</summary>
    public DateTime At { get; set; }

    /// <summary>时间段结束（截止条目用于画条块；无则等于 At）。</summary>
    public DateTime? EndAt { get; set; }

    /// <summary>学校名称（任务归属学校的档案名）。</summary>
    public string? SchoolName { get; set; }

    /// <summary>发布状态：0 草稿 / 1 已发布。</summary>
    public int PublishStatus { get; set; }

    public int AssignmentCount { get; set; }
    public int CompletedCount { get; set; }
    public int NeedsHelpCount { get; set; }

    /// <summary>是否已过截止时间且仍有未提交学生。</summary>
    public bool IsOverdue { get; set; }
}

/// <summary>课表数据（含区间汇总，前端直接渲染日/周/月）。</summary>
public class ClassroomTimetableDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalCount { get; set; }
    public int TaskCount { get; set; }
    public int CourseCount { get; set; }
    public int OverdueCount { get; set; }
    public List<ClassroomTimetableItemDto> Items { get; set; } = new();
    public List<ClassroomTimetableCourseDto> Courses { get; set; } = new();
}

public class ClassroomTimetableCourseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TaskCount { get; set; }
}
