using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighProject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighProjectStatus Status { get; set; } = DoubleHighProjectStatus.Draft;
    public DateTime? StartTime { get; set; }

    /// <summary>填报截止时间（原型中的「填报截止」）。</summary>
    public DateTime? EndTime { get; set; }

    public DateTime? LastCollectedAt { get; set; }

    /// <summary>
    /// 评估模板 Key（course / major / team / custom）。
    /// 决定「新建评估项目」时预置哪些指标；为空表示校本自定义项目。
    /// </summary>
    public string? TemplateKey { get; set; }

    /// <summary>评估对象，如「课程《数据库原理与应用》」「专业：计算机应用技术」。</summary>
    public string? AssessmentTarget { get; set; }

    /// <summary>填报人姓名（教师自行创建项目时冗余记录，便于导出资料署名）。</summary>
    public string? ReporterName { get; set; }

    /// <summary>提交时间（教师点击「提交填报」时写入）。</summary>
    public DateTime? SubmittedAt { get; set; }

    public DoubleHighProject()
    {
    }

    public DoubleHighProject(Guid id, string title, string batchCode)
        : base(id)
    {
        Title = title;
        BatchCode = batchCode;
    }
}
