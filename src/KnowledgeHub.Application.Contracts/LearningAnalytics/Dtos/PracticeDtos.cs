using System;
using System.Collections.Generic;
using KnowledgeHub.Exams.Enums;

namespace KnowledgeHub.LearningAnalytics.Dtos;

/// <summary>学生端「专项练习」题集</summary>
public class PracticeSetDto
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public Guid? KnowledgePointId { get; set; }
    public string? KnowledgePointName { get; set; }
    /// <summary>推荐理由（如「级数收敛判别 掌握率 32%（低于目标 75%）」）</summary>
    public string? Reason { get; set; }
    public List<PracticeExerciseDto> Items { get; set; } = new();
    /// <summary>无推荐题时的提示</summary>
    public string? Message { get; set; }
}

public class PracticeExerciseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public ExerciseType Type { get; set; }
    public string? Options { get; set; }
    public int Score { get; set; }
    public int Difficulty { get; set; }
    public string? ChapterTitle { get; set; }
    /// <summary>知识点名（用于结果页归因）</summary>
    public List<string> KnowledgePointNames { get; set; } = new();
}

public class SubmitPracticeInput
{
    public Guid CourseId { get; set; }
    public Guid? KnowledgePointId { get; set; }
    public List<PracticeAnswerInput> Answers { get; set; } = new();
}

public class PracticeAnswerInput
{
    public Guid ExerciseId { get; set; }
    public string? Answer { get; set; }
    public long TimeSpentTicks { get; set; }
}

public class PracticeResultDto
{
    public int TotalCount { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    /// <summary>本次练习涉及知识点的最新掌握率（已回写）</summary>
    public List<KnowledgeMasteryItemDto> KnowledgePoints { get; set; } = new();
    /// <summary>本次每题对错（用于结果页逐题回顾）</summary>
    public List<PracticeItemResultDto> Items { get; set; } = new();
}

public class PracticeItemResultDto
{
    public Guid ExerciseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? StudentAnswer { get; set; }
    public string? CorrectAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public string? Analysis { get; set; }
}
