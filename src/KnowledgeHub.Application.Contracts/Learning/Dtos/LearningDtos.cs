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
    /// <summary>该课程的一级章节数（ParentId == null），与课程列表口径一致。</summary>
    public int ChapterCount { get; set; }
    /// <summary>该课程学时（与 Course.SemesterHours 同源）。</summary>
    public int? SemesterHours { get; set; }
    /// <summary>该学生已学习（已提交）的章节习题数（去重）。</summary>
    public int CompletedExerciseCount { get; set; }
    /// <summary>该课程全部可学习的章节习题总数（已关联章节，去重）。</summary>
    public int TotalExerciseCount { get; set; }
}

public class CourseExerciseProgressDto
{
    public Guid CourseId { get; set; }
    /// <summary>该学生已学习（已提交）的章节习题数（去重）。</summary>
    public int CompletedCount { get; set; }
    /// <summary>该课程全部可学习的章节习题总数（已关联章节，去重）。</summary>
    public int TotalCount { get; set; }
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
    /// <summary>最近 7 天日期标签（MM-dd）。</summary>
    public List<string> DailyTimeLabels { get; set; } = new();
    /// <summary>
    /// 最近 7 天每日学习「活动次数」（习题提交 + 资源学习），不是分钟数。
    /// 字段名为历史遗留，语义以本注释为准。
    /// </summary>
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
