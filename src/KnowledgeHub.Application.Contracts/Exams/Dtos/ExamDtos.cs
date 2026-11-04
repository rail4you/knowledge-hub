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

// ══════════════════════════════════════════════════════════════
//  组卷 / 答题 / 判卷（新增）
// ══════════════════════════════════════════════════════════════

/// <summary>试卷列表查询（教师端）。</summary>
public class ExamListQueryDto : PagedAndSortedResultRequestDto
{
    public Guid? CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public ExamType? Type { get; set; }
    public string? Filter { get; set; }
}

/// <summary>教师端试卷列表项（含提交/待判数量，便于一眼看出待办）。</summary>
public class ExamListItemDto : ExamDto
{
    public string? CourseName { get; set; }
    public int SubmissionCount { get; set; }
    public int PendingGradeCount { get; set; }
    public int GradedCount { get; set; }
}

/// <summary>试卷中的一道题（教师视角，含标准答案与解析）。</summary>
public class ExamQuestionDto
{
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public int Score { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? QuestionAnalysis { get; set; }
    public int Difficulty { get; set; }
}

/// <summary>试卷详情（教师视角）。</summary>
public class ExamDetailDto : ExamDto
{
    public string? CourseName { get; set; }
    public List<ExamQuestionDto> Questions { get; set; } = new();

    /// <summary>最近一次组卷使用的规则（总分模式组卷时用于回显）。</summary>
    public List<ExamTypeRuleDto>? Rules { get; set; }
}

/// <summary>组卷规则：按题型指定数量与每题分值。</summary>
public class ExamTypeRuleDto
{
    public ExerciseType Type { get; set; }

    /// <summary>抽取题数。</summary>
    public int Count { get; set; }

    /// <summary>每题分值；0 表示沿用习题自身的分值。</summary>
    public int ScorePerItem { get; set; }
}

/// <summary>新建 / 编辑试卷（手工选题）。</summary>
public class CreateExamDto
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

/// <summary>自动组卷入参：按题型 + 每题分 + 数量，或按目标总分自动配平。</summary>
public class GenerateExamInput
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }

    /// <summary>
    /// 限定章节（可多选）。为空时按整门课程抽题；
    /// 有值时按「章节维度」取题：习题主章节命中任一章节，或章节多对多关联命中任一章节。
    /// <see cref="ChapterId"/> 仍作为主章节记录在试卷上（兼容单章节场景与列表筛选）。
    /// </summary>
    public List<Guid>? ChapterIds { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ExamType Type { get; set; } = ExamType.Quiz;
    public int DurationMinutes { get; set; } = 60;

    /// <summary>及格分（按最终总分百分比换算前使用；&lt;=100 时按百分比处理，&gt;100 视为绝对分值）。</summary>
    public int PassingScore { get; set; } = 60;

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>精细模式：题型 + 数量 + 每题分。</summary>
    public List<ExamTypeRuleDto> Rules { get; set; } = new();

    /// <summary>总分模式：给定目标总分，按题型池自动抽题直至达标（Rules 只作为题型范围参考）。</summary>
    public int? TargetTotalScore { get; set; }

    /// <summary>是否打乱题序（默认打乱）。</summary>
    public bool Shuffle { get; set; } = true;
}

/// <summary>学生端：我的试卷（含我的作答状态）。</summary>
public class StudentExamListItemDto
{
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ExamType Type { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalScore { get; set; }
    public int PassingScore { get; set; }
    public int ExerciseCount { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>我的作答记录（未开始为 null）。</summary>
    public Guid? StudentExamId { get; set; }
    public ExamStatus Status { get; set; } = ExamStatus.NotStarted;
    public int? MyScore { get; set; }
    public bool? IsPassed { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>学生答题卷（不含标准答案）。</summary>
public class StudentExamPaperDto
{
    public Guid StudentExamId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalScore { get; set; }
    public int PassingScore { get; set; }
    public ExamStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public List<ExamPaperQuestionDto> Questions { get; set; } = new();
}

/// <summary>答题卷中的题目（学生视角）。</summary>
public class ExamPaperQuestionDto
{
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public int Score { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public string? Options { get; set; }
    public int Difficulty { get; set; }

    /// <summary>我的答案（继续作答时回填）。</summary>
    public string? MyAnswer { get; set; }
}

/// <summary>判卷结果（学生 / 教师共用；学生视角不返回标准答案）。</summary>
public class StudentExamResultDto
{
    public Guid StudentExamId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public ExamStatus Status { get; set; }
    public int? TotalScore { get; set; }
    public int MaxScore { get; set; }
    public bool? IsPassed { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public List<StudentAnswerDetailDto> Answers { get; set; } = new();
}

/// <summary>逐题作答与得分明细。</summary>
public class StudentAnswerDetailDto
{
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public string? Options { get; set; }
    public int MaxScore { get; set; }
    public string? Content { get; set; }
    public string? StandardAnswer { get; set; }
    public string? QuestionAnalysis { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public bool? IsCorrect { get; set; }
    public bool IsAiGraded { get; set; }
}

/// <summary>教师端提交列表查询。</summary>
public class ExamSubmissionQueryDto : PagedAndSortedResultRequestDto
{
    public Guid? ExamId { get; set; }
    public Guid? CourseId { get; set; }
    public ExamStatus? Status { get; set; }
    public string? Filter { get; set; }
}

/// <summary>教师端提交列表项。</summary>
public class ExamSubmissionItemDto
{
    public Guid StudentExamId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public ExamStatus Status { get; set; }
    public int? TotalScore { get; set; }
    public int MaxScore { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public int SubjectiveCount { get; set; }
}

/// <summary>人工判卷入参。</summary>
public class GradeSubmissionInput
{
    public List<GradeAnswerItemDto> Answers { get; set; } = new();
    public string? Comment { get; set; }
}

public class GradeAnswerItemDto
{
    public Guid ExerciseId { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
}
