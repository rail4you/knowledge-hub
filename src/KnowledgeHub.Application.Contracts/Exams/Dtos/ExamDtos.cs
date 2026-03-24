using System;
using System.Collections.Generic;
using KnowledgeHub.Exams.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Exams.Dtos;

public class ExamDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ExamType Type { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalScore { get; set; }
    public int PassingScore { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ExerciseCount { get; set; }
}

public class CreateUpdateExamDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ExamType Type { get; set; } = ExamType.Quiz;
    public int DurationMinutes { get; set; } = 60;
    public int PassingScore { get; set; } = 60;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ExamExerciseItemDto> Exercises { get; set; } = new();
}

public class ExamExerciseItemDto
{
    public Guid ExerciseId { get; set; }
    public int Score { get; set; }
    public int SortOrder { get; set; }
}

public class StudentExamDto : FullAuditedEntityDto<Guid>
{
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public ExamStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? TotalScore { get; set; }
    public bool? IsPassed { get; set; }
    public List<StudentAnswerDto> Answers { get; set; } = new();
}

public class SubmitExamInput
{
    public List<SubmitAnswerItem> Answers { get; set; } = new();
}

public class SubmitAnswerItem
{
    public Guid ExerciseId { get; set; }
    public string? Content { get; set; }
}

public class StudentAnswerDto
{
    public Guid ExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public bool? IsCorrect { get; set; }
}
