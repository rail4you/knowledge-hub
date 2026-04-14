using System;
using System.Collections.Generic;
using KnowledgeHub.Exams.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Exams.Dtos;

public class ExerciseDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? AnswerExplanation { get; set; }
    public int Difficulty { get; set; }
    public int Score { get; set; }
    public bool IsAiGenerated { get; set; }
}

public class CreateUpdateExerciseDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; } = ExerciseType.SingleChoice;
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? AnswerExplanation { get; set; }
    public int Difficulty { get; set; } = 1;
    public int Score { get; set; } = 1;
}

public class GenerateExerciseInput
{
    public Guid CourseId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public ExerciseType Type { get; set; }
    public int Count { get; set; } = 5;
    public int Difficulty { get; set; } = 2;
    public string? TopicHint { get; set; }
}

public class GradeEssayInput
{
    public Guid StudentExamId { get; set; }
    public Guid ExerciseId { get; set; }
    public string StandardAnswer { get; set; } = string.Empty;
    public int MaxScore { get; set; }
}

public class GradingResultDto
{
    public int Score { get; set; }
    public string? Feedback { get; set; }
    public bool IsCorrect { get; set; }
}

/// <summary>
/// 习题导入结果 DTO
/// </summary>
public class ExerciseImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
