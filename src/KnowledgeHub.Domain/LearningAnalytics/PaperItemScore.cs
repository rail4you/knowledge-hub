using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>试卷单题得分（映射到应用内真实习题）。</summary>
public class PaperItemScore : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ScriptId { get; set; }

    public Guid BatchId { get; set; }

    /// <summary>卷面题号，如 "5"</summary>
    public string QuestionNo { get; set; } = string.Empty;

    /// <summary>匹配到的题库原题（未匹配时为空）</summary>
    public Guid? ExerciseId { get; set; }

    /// <summary>匹配到的章节（用于按章节归因）</summary>
    public Guid? ChapterId { get; set; }

    public PaperMatchStatus MatchStatus { get; set; } = PaperMatchStatus.None;

    public double Score { get; set; }

    public double FullScore { get; set; } = 1;

    /// <summary>单题识别置信度</summary>
    public double OcrConfidence { get; set; } = 1;

    /// <summary>OCR 识别到的题干片段，用于人工比对</summary>
    public string? RawText { get; set; }

    private PaperItemScore() { }

    public PaperItemScore(Guid id, Guid scriptId, Guid batchId, string questionNo) : base(id)
    {
        ScriptId = scriptId;
        BatchId = batchId;
        QuestionNo = questionNo;
    }

    public double Ratio => FullScore > 0 ? Math.Clamp(Score / FullScore, 0, 1) : 0;
}
