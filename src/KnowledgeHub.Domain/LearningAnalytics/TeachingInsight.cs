using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>章节教学诊断结论（由学生成绩反推教学环节）。</summary>
public class TeachingInsight : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? PaperBatchId { get; set; }

    public Guid? ChapterId { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    /// <summary>章节得分率 0~1</summary>
    public double AverageRate { get; set; }

    public double StdDev { get; set; }

    /// <summary>前 25% 学生的平均得分率（用于识别「连高分学生也失分」）</summary>
    public double TopQuartileRate { get; set; }

    public int SampleSize { get; set; }

    public DiagnosisLevel Diagnosis { get; set; }

    public string? Advice { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    private TeachingInsight() { }

    public TeachingInsight(Guid id, Guid courseId, string chapterTitle) : base(id)
    {
        CourseId = courseId;
        ChapterTitle = chapterTitle;
    }
}
