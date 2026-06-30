using System;
using System.Collections.Generic;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Learning.Dtos;

public class StudentCourseDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public string? Semester { get; set; }
    public StudentCourseStatus Status { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal Progress { get; set; }
    public DateTime? LastLearnedAt { get; set; }
    public int StudyMinutes { get; set; }
}

public class StudentCourseListItemDto : EntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string? CourseCoverImageUrl { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public string? Semester { get; set; }
    public StudentCourseStatus Status { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal Progress { get; set; }
    public int? Credits { get; set; }
}

public class LearningProgressDto : EntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? ResourceId { get; set; }
    public decimal Progress { get; set; }
    public string? LastPosition { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public DateTime LastAccessAt { get; set; }
}

public class RecordProgressInput
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? ResourceId { get; set; }
    public decimal Progress { get; set; }
    public string? LastPosition { get; set; }
    public int AdditionalMinutes { get; set; }
}

public class KnowledgeMasteryDto : EntityDto<Guid>
{
    public Guid KnowledgeResourceId { get; set; }
    public string KnowledgeResourceName { get; set; } = string.Empty;
    public MasteryLevel Level { get; set; }
    public int PracticeCount { get; set; }
    public int CorrectCount { get; set; }
    public decimal Accuracy { get; set; }
    public DateTime LastPracticeAt { get; set; }
}

public class LearningDashboardDto
{
    public int TotalCourses { get; set; }
    public int CompletedCourses { get; set; }
    public int InProgressCourses { get; set; }
    public int NotStartedCourses { get; set; }
    public decimal TotalLearningTime { get; set; }
    public int TotalExerciseRecords { get; set; }
    public int TotalResourceActivities { get; set; }
    public decimal AverageProgress { get; set; }
    public List<string> DailyTimeLabels { get; set; } = new();
    public List<decimal> DailyTimeValues { get; set; } = new();
    public List<KnowledgeDimensionDto> KnowledgeDimensions { get; set; } = new();
    public List<decimal> MasteryValues { get; set; } = new();
    public List<RecentLearningDto> RecentLearning { get; set; } = new();
}

public class KnowledgeDimensionDto
{
    public string Name { get; set; } = string.Empty;
    public int MaxValue { get; set; } = 100;
}

public class RecentLearningDto
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public decimal Progress { get; set; }
    public DateTime LastAccessAt { get; set; }
}
