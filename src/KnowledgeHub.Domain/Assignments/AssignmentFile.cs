using System;
using KnowledgeHub.Assignments.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 作业文件：既承载教师上传的「作业要求附件」，也承载学生上传的「提交件」
/// 与教师回传的「批注版」。
///
/// 三类文件共用一张表，是为了让下载接口（按文件 Id 取流）保持单一入口，
/// 权限校验也只需在一处做。
/// </summary>
public class AssignmentFile : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属作业</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>所属提交（要求附件 / 尚未提交的暂存文件为空）</summary>
    public Guid? SubmissionId { get; set; }

    /// <summary>文件用途</summary>
    public AssignmentFileType Type { get; set; }

    /// <summary>原始文件名</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>存储相对路径</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>MIME 类型</summary>
    public string? ContentType { get; set; }

    /// <summary>提交版本号（学生第几次提交；要求附件为 0）</summary>
    public int Version { get; set; }

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    private AssignmentFile()
    {
    }

    public AssignmentFile(
        Guid id,
        Guid assignmentId,
        AssignmentFileType type,
        string fileName,
        string filePath,
        long fileSize,
        string? contentType = null,
        int version = 0,
        int sortOrder = 0) : base(id)
    {
        AssignmentId = assignmentId;
        Type = type;
        FileName = fileName;
        FilePath = filePath;
        FileSize = fileSize;
        ContentType = contentType;
        Version = version;
        SortOrder = sortOrder;
    }
}
