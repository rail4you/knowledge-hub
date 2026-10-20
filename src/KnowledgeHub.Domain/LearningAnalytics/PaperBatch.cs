using System;
using System.Collections.Generic;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>一次纸质试卷上传批次（如「2023秋 期末考试」）。</summary>
public class PaperBatch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTime ExamDate { get; set; } = DateTime.UtcNow;

    public PaperBatchStatus Status { get; set; } = PaperBatchStatus.Draft;

    public int ScriptCount { get; set; }

    public int NeedsConfirmCount { get; set; }

    public int PushedCount { get; set; }

    public double AverageScore { get; set; }

    /// <summary>上传的文件名（Mock 阶段仅记录，不落存储）</summary>
    public string? SourceFileNames { get; set; }

    /// <summary>OCR 是否使用 Mock（当前恒为 true）</summary>
    public bool IsMockOcr { get; set; } = true;

    public ICollection<PaperScript> Scripts { get; set; } = new List<PaperScript>();

    private PaperBatch() { }

    public PaperBatch(Guid id, Guid courseId, string courseName, string title) : base(id)
    {
        CourseId = courseId;
        CourseName = courseName;
        Title = title;
    }
}
