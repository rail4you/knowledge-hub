using System;
using System.Collections.Generic;
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
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TeachingAgentVisibility Visibility { get; set; } = TeachingAgentVisibility.Private;
    public string SystemPrompt { get; set; } = string.Empty;
    public string? WelcomeMessage { get; set; }
    public string ModelId { get; set; } = "qwen-plus";
    public double Temperature { get; set; } = 0.2;
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
}

public class CourseOptionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class ResourceOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
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
    public string? Major { get; set; }
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
    public string? AnswerExplanation { get; set; }
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
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TeachingAgentVersionId { get; set; }
    public string TaskPrompt { get; set; } = string.Empty;
    public ClassroomAgentTaskTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
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
}

public class ClassroomAgentTaskDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TeachingAgentId { get; set; }
    public Guid TeachingAgentVersionId { get; set; }
    public string TeachingAgentName { get; set; } = string.Empty;
    public int TeachingAgentVersionNumber { get; set; }
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
    public string Summary { get; set; } = string.Empty;
}

public class NeedTeacherHelpDto
{
    public string Reason { get; set; } = string.Empty;
}

public class SendAgentRunMessageDto
{
    public string Message { get; set; } = string.Empty;
}

public class AgentMessageChunkDto
{
    public string Content { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}
