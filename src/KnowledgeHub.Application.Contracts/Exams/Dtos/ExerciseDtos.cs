using System;
using System.Collections.Generic;
using KnowledgeHub.Exams.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Exams.Dtos;

public class ExerciseDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public List<Guid> ChapterIds { get; set; } = new();
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? QuestionAnalysis { get; set; }
    public int Difficulty { get; set; }
    public int Score { get; set; }
    public bool IsAiGenerated { get; set; }
}

public class CreateUpdateExerciseDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public List<Guid> ChapterIds { get; set; } = new();
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; } = ExerciseType.SingleChoice;
    public string? Options { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? QuestionAnalysis { get; set; }
    public int Difficulty { get; set; } = 1;
    public int Score { get; set; } = 1;
}

public class GenerateExerciseInput
{
    public Guid CourseId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    /// <summary>主章节（生成的习题归属，写入 Exercise.ChapterId）</summary>
    public Guid? ChapterId { get; set; }
    /// <summary>全量章节（含主章节，写入 ChapterExercise 关联表）</summary>
    public List<Guid> ChapterIds { get; set; } = new();
    public ExerciseType Type { get; set; }
    public int Count { get; set; } = 5;
    public int Difficulty { get; set; } = 2;
    public string? TopicHint { get; set; }
    /// <summary>教师自定义提示词（出题方向/知识点侧重/语言风格等）</summary>
    public string? CustomPrompt { get; set; }
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

public class AiAnalyzeExerciseInput
{
    public List<Guid> ExerciseIds { get; set; } = new();
}

public class AiAnalyzeExerciseResultDto
{
    public int UpdatedCount { get; set; }
    public int TotalCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
