using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 试卷图片（一张图片 = 一份学生试卷）。
/// 文件落盘到本地目录，OCR 结果与识别状态记在这里，便于后台任务重试与审计。
/// </summary>
public class PaperUploadImage : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid BatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>相对存储根目录的路径</summary>
    public string StoredPath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>PDF 拆页后的页码（从 1 开始；图片固定为 1）</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 合并分组：同一份多页试卷的多张图片共享同一个分组键（通常是原 PDF 文件名）。
    /// 为空表示「一张图片 = 一份学生试卷」。
    /// </summary>
    public string? MergeGroup { get; set; }

    /// <summary>
    /// 所属学生：教师在上传时手工指定（来自该课程的选课学生），
    /// 因此 OCR 不再需要从卷面识别学号，识别结果直接归属到该学生。
    /// </summary>
    public Guid? StudentId { get; set; }

    /// <summary>所属学生姓名（冗余存储，便于未关联到用户时也能展示）</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>所属学生学号</summary>
    public string StudentNumber { get; set; } = string.Empty;

    public PaperUploadStatus Status { get; set; } = PaperUploadStatus.Pending;

    public string? OcrProvider { get; set; }

    public string? OcrModel { get; set; }

    /// <summary>模型返回的原始 JSON / 文本，失败时保存错误信息</summary>
    public string? OcrRawJson { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>识别成功后生成的试卷 Id</summary>
    public Guid? ScriptId { get; set; }

    public DateTime? ProcessedAt { get; set; }

    private PaperUploadImage() { }

    public PaperUploadImage(Guid id, Guid batchId, string fileName, string storedPath, long sizeBytes) : base(id)
    {
        BatchId = batchId;
        FileName = fileName;
        StoredPath = storedPath;
        SizeBytes = sizeBytes;
    }
}
