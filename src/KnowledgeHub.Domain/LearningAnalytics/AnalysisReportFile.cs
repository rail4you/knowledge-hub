using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 已归档的分析报告文件（Word / Excel）。
/// 用于留档与审计：记录生成时间、文件大小、下载次数，便于历史对比与追溯。
/// </summary>
public class AnalysisReportFile : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid BatchId { get; set; }

    public Guid CourseId { get; set; }

    public AnalysisReportFileType Type { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>相对存储根目录的路径</summary>
    public string StoredPath { get; set; } = string.Empty;

    public int DownloadCount { get; set; }

    public DateTime? LastDownloadedAt { get; set; }

    /// <summary>生成时的关键指标快照（JSON，便于历史对比，如平均分/达标率）</summary>
    public string? MetricsSnapshot { get; set; }

    private AnalysisReportFile() { }

    public AnalysisReportFile(Guid id, Guid batchId, Guid courseId, AnalysisReportFileType type) : base(id)
    {
        BatchId = batchId;
        CourseId = courseId;
        Type = type;
    }

    public void MarkDownloaded()
    {
        DownloadCount++;
        LastDownloadedAt = DateTime.UtcNow;
    }
}
