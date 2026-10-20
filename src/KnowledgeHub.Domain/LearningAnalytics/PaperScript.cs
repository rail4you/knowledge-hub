using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>单份试卷（OCR 识别结果 + 人工确认状态）。</summary>
public class PaperScript : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid BatchId { get; set; }

    /// <summary>匹配到的学生（按学号识别，可为空表示未匹配到系统学生）</summary>
    public Guid? StudentId { get; set; }

    public string StudentNumber { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public double TotalScore { get; set; }

    /// <summary>整卷 OCR 置信度（0~1）</summary>
    public double OcrConfidence { get; set; } = 1;

    /// <summary>OCR 提供方（qwen / mock / manual）</summary>
    public string? OcrProvider { get; set; }

    /// <summary>OCR 模型名</summary>
    public string? OcrModel { get; set; }

    /// <summary>来源图片文件名</summary>
    public string? SourceFileName { get; set; }

    /// <summary>模型原始输出（JSON 文本），用于复盘与重算</summary>
    public string? RawOcrJson { get; set; }

    /// <summary>是否已人工确认（确认后才写入 AnswerRecord）</summary>
    public bool Confirmed { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public ICollection<PaperItemScore> Items { get; set; } = new List<PaperItemScore>();

    private PaperScript() { }

    public PaperScript(Guid id, Guid batchId, string studentNumber, string studentName) : base(id)
    {
        BatchId = batchId;
        StudentNumber = studentNumber;
        StudentName = studentName;
    }
}
