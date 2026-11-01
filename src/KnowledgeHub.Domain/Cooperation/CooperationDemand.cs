using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Cooperation;

/// <summary>
/// 产教合作需求（企业侧发起）。
///
/// <para>
/// 生命周期：草稿 → 提交审核（学校初审 → 联盟终审）→ 已发布（进入需求大厅）→ 关闭 / 归档。
/// 审核通过前不得进入大厅；<b>需求发布不等于产生资源库权限</b>（设计方案 13.1）。
/// </para>
/// </summary>
public class CooperationDemand : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxTitleLength = 300;
    public const int MaxTextLength = 4000;
    public const int MaxRegionLength = 128;
    public const int MaxContactNameLength = 128;
    public const int MaxContactPhoneLength = 64;
    public const int MaxContactEmailLength = 256;

    public Guid EnterpriseProfileId { get; set; }

    public CooperationDemandType Type { get; set; } = CooperationDemandType.TalentTraining;

    public string Title { get; set; } = string.Empty;

    /// <summary>专业方向（逗号分隔，便于筛选）。</summary>
    public string? Majors { get; set; }

    public string? Industries { get; set; }

    public string? Region { get; set; }

    public string? Background { get; set; }

    public string? EnterpriseInput { get; set; }

    public string? SchoolConditions { get; set; }

    public string? Cycle { get; set; }

    public string? Scale { get; set; }

    public string? BudgetNote { get; set; }

    public string? DeliveryAcceptance { get; set; }

    public string? IpTerms { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public CooperationDemandScope Scope { get; set; } = CooperationDemandScope.HostSchoolOnly;

    public CooperationDemandStatus Status { get; set; } = CooperationDemandStatus.Draft;

    public DateTime? PublishedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? RejectReason { get; set; }

    /// <summary>入驻校租户（ABP 过滤维度）。</summary>
    public Guid? TenantId { get; set; }

    public ICollection<CooperationResponse> Responses { get; set; } = new List<CooperationResponse>();

    public void SetTitle(string title)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), MaxTitleLength);
    }

    public void MarkPendingReview()
    {
        Status = CooperationDemandStatus.PendingReview;
        RejectReason = null;
    }

    public void MarkPublished()
    {
        Status = CooperationDemandStatus.Published;
        PublishedAt = DateTime.UtcNow;
        RejectReason = null;
    }

    public void MarkRejected(string? reason)
    {
        Status = CooperationDemandStatus.Rejected;
        RejectReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim().Length > 500 ? reason.Trim()[..500] : reason.Trim();
    }

    public void Close()
    {
        Status = CooperationDemandStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = CooperationDemandStatus.Archived;
    }
}

/// <summary>学校对合作需求的响应（意向）。</summary>
public class CooperationResponse : CreationAuditedEntity<Guid>, IMultiTenant
{
    public const int MaxNoteLength = 2000;

    public CooperationResponse()
    {
    }

    public CooperationResponse(Guid id)
        : base(id)
    {
    }


    public Guid DemandId { get; set; }

    /// <summary>响应学校租户。</summary>
    public Guid SchoolTenantId { get; set; }

    public Guid ResponderUserId { get; set; }

    public string? ResponderName { get; set; }

    public string? Note { get; set; }

    public CooperationResponseStatus Status { get; set; } = CooperationResponseStatus.Submitted;

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    /// <summary>响应学校租户（ABP 过滤维度：响应归属响应方学校）。</summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 合作项目台账（校企双方确认后形成）。
///
/// <para>
/// 归属主责学校租户（<see cref="TenantId"/>），企业侧通过 <see cref="EnterpriseProfileId"/> 关联。
/// 平台阶段记录<b>不能</b>代替正式合同 / 实习协议 / 知识产权许可（设计方案 13.2）。
/// </para>
/// </summary>
public class CooperationProject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxNameLength = 300;
    public const int MaxAgreementRefLength = 500;

    public Guid? DemandId { get; set; }

    public Guid? ResponseId { get; set; }

    public Guid EnterpriseProfileId { get; set; }

    public Guid SchoolTenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public CooperationProjectStage Stage { get; set; } = CooperationProjectStage.Confirming;

    /// <summary>协议 / 确认材料引用（文件 Id 或编号，不是合同正文）。</summary>
    public string? AgreementRef { get; set; }

    public Guid? OwnerUserId { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public string? AcceptanceNote { get; set; }

    public Guid? TenantId { get; set; }

    public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MaxNameLength);
    }

    public void AdvanceTo(CooperationProjectStage stage)
    {
        Stage = stage;
    }
}

/// <summary>项目里程碑。</summary>
public class ProjectMilestone : Entity<Guid>
{
    public const int MaxNameLength = 200;

    public ProjectMilestone()
    {
    }

    public ProjectMilestone(Guid id)
        : base(id)
    {
    }

    public const int MaxEvidenceRefLength = 500;

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime? DueAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? EvidenceRef { get; set; }

    public string? Note { get; set; }

    public int SortOrder { get; set; }

    public bool IsCompleted => CompletedAt.HasValue;

    public void Complete(string? evidenceRef, string? note)
    {
        CompletedAt = DateTime.UtcNow;
        EvidenceRef = evidenceRef;
        Note = note;
    }
}
