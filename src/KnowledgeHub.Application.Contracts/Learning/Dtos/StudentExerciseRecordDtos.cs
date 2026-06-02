using System;
using System.Collections.Generic;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Learning.Dtos;

public class StudentExerciseRecordDto : FullAuditedEntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterName { get; set; }
    public Guid ExerciseId { get; set; }
    public string? ExerciseTitle { get; set; }
    public string? StudentAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public bool HasViewedAnswer { get; set; }
    public DateTime? ViewedAt { get; set; }
    public SelfAssessment SelfAssessment { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SaveExerciseRecordInput
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid ExerciseId { get; set; }
    public string? StudentAnswer { get; set; }
    public long TimeSpentTicks { get; set; }
}

public class MarkAnswerViewedInput
{
    public Guid ExerciseId { get; set; }
    public Guid CourseId { get; set; }
}

public class SubmitSelfAssessmentInput
{
    public Guid ExerciseId { get; set; }
    public Guid CourseId { get; set; }
    public SelfAssessment Assessment { get; set; }
}

public class GetStudentExerciseRecordsInput : PagedAndSortedResultRequestDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
}

public class GetMyRecentRecordsInput : PagedAndSortedResultRequestDto
{
    public Guid? CourseId { get; set; }
    /// <summary>
    /// 0=wrong, 1=correct. Null=no filter.
    /// </summary>
    public int? IsCorrect { get; set; }
}

public class StudentLearningStatisticsDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal CorrectRate { get; set; }
    public TimeSpan TotalTimeSpent { get; set; }
    public DateTime? LastActiveTime { get; set; }
}

public class CourseLearningOverviewDto
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int TotalExercises { get; set; }
    public decimal AverageCompletionRate { get; set; }
    public decimal AverageCorrectRate { get; set; }
    public List<ChapterProgressDto> ChapterProgress { get; set; } = new();
}

public class ChapterProgressDto
{
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = string.Empty;
    public int TotalExercises { get; set; }
    public int CompletedCount { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal CorrectRate { get; set; }
}

public class GetLearningStatisticsInput : PagedAndSortedResultRequestDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class GetCourseLearningOverviewInput
{
    public Guid CourseId { get; set; }
    public Guid? TenantId { get; set; }
}
