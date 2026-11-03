using System;
using System.Collections.Generic;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.DoubleHigh.Dtos;

public class DoubleHighIndicatorValueSnapshotDto
{
    public Guid IndicatorId { get; set; }
    public decimal Value { get; set; }

    /// <summary>文字填报内容（填报类型为「填写说明」时使用）。</summary>
    public string? TextValue { get; set; }

    public string? Note { get; set; }
    public DoubleHighValueSourceType SourceType { get; set; }
    public DateTime CollectedAt { get; set; }
}

public class DoubleHighIndicatorDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string IndicatorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public DoubleHighDataSourceType DataSourceType { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }
    public DoubleHighIndicatorValueSnapshotDto? LatestValue { get; set; }

    /// <summary>填报类型：系统取数 / 填写数值 / 填写说明 / 仅佐证。</summary>
    public DoubleHighIndicatorType IndicatorType { get; set; }

    /// <summary>评分规则：比例制 / 达标制 / 直接计分 / 不计分。</summary>
    public DoubleHighScoringRule ScoringRule { get; set; }

    /// <summary>必备佐证清单（每一项都至少要上传一个文件才算完成）。</summary>
    public List<string> RequiredEvidences { get; set; } = new();

    /// <summary>设置依据（观测点 / 校本说明）。</summary>
    public string? Basis { get; set; }

    /// <summary>数据来源的文字描述。</summary>
    public string? DataSourceName { get; set; }

    /// <summary>取值范围类型（仅系统取数指标）：为空表示「随项目对象」。</summary>
    public DoubleHighObjectType? ScopeType { get; set; }

    /// <summary>取值范围 id（资源库 / 专业 / 课程）。</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>取值范围名称。</summary>
    public string? ScopeName { get; set; }

    /// <summary>实际取值范围的展示文案（如「资源库：X」「专业：X」「全校（未限定）」）。</summary>
    public string ScopeLabel { get; set; } = string.Empty;

    /// <summary>引用的项目依据文件 id。</summary>
    public Guid? BasisDocumentId { get; set; }

    /// <summary>引用的项目依据文件名称。</summary>
    public string? BasisDocumentTitle { get; set; }

    /// <summary>引用的项目依据文件地址（可直接打开 / 预览）。</summary>
    public string? BasisDocumentUrl { get; set; }

    /// <summary>完成度（0~100），按评分规则折算；不计分指标为 0。</summary>
    public decimal Progress { get; set; }

    /// <summary>该指标折算后的加权得分（权重 × 完成度）。</summary>
    public decimal Score { get; set; }

    /// <summary>该指标是否已填报完成（含必备佐证是否齐全）。</summary>
    public bool Completed { get; set; }

    /// <summary>完成状态文案：待填写 / 待补佐证 / 已完成。</summary>
    public string CompletionStatus { get; set; } = string.Empty;

    /// <summary>该指标已上传的佐证文件数。</summary>
    public int EvidenceCount { get; set; }
}

public class DoubleHighEvidenceDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid IndicatorId { get; set; }
    public string? IndicatorName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighEvidenceType EvidenceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? ExternalLink { get; set; }
    public int SortOrder { get; set; }

    /// <summary>佐证名称（对应必备佐证清单中的某一项）。</summary>
    public string? EvidenceName { get; set; }

    /// <summary>上传文件的原始文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>上传文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>是否为教师上传的佐证文件（true 时可随资料包一起导出）。</summary>
    public bool IsFile { get; set; }
}

public class DoubleHighReportDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string? SummaryJson { get; set; }
    public Guid? GeneratedById { get; set; }
    public string? GeneratedByName { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class DoubleHighDashboardDto
{
    public int TotalIndicators { get; set; }
    public int ManualIndicators { get; set; }
    public int AutomaticIndicators { get; set; }
    public int CollectedIndicators { get; set; }

    /// <summary>已完成填报的指标数（按指标逐项判定，含必备佐证是否齐全）。</summary>
    public int CompletedIndicators { get; set; }

    public int EvidenceCount { get; set; }
    public decimal CompletionRate { get; set; }

    /// <summary>按权重加权的总分（0~100，不计分指标不参与）。</summary>
    public decimal TotalScore { get; set; }

    public DateTime? LastCollectedAt { get; set; }
}

public class DoubleHighProjectDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? LastCollectedAt { get; set; }
    public int IndicatorCount { get; set; }
    public int CollectedIndicatorCount { get; set; }
    public int EvidenceCount { get; set; }
    public decimal CompletionRate { get; set; }

    /// <summary>评估模板 Key（course / major / team / custom）。</summary>
    public string? TemplateKey { get; set; }

    /// <summary>评估模板名称。</summary>
    public string? TemplateName { get; set; }

    /// <summary>评估对象。</summary>
    public string? AssessmentTarget { get; set; }

    /// <summary>填报人。</summary>
    public string? ReporterName { get; set; }

    /// <summary>提交时间。</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>评估对象类型（教学资源库 / 专业 / 课程 / 手动填写）。</summary>
    public DoubleHighObjectType ObjectType { get; set; }

    /// <summary>评估对象 id。</summary>
    public Guid? ObjectId { get; set; }

    /// <summary>评估对象名称。</summary>
    public string? ObjectName { get; set; }

    /// <summary>已完成填报的指标数。</summary>
    public int CompletedIndicatorCount { get; set; }

    /// <summary>按权重加权的总分（0~100）。</summary>
    public decimal TotalScore { get; set; }
}

public class DoubleHighProjectDetailDto : DoubleHighProjectDto
{
    public DoubleHighDashboardDto Dashboard { get; set; } = new();
    public List<DoubleHighIndicatorDto> Indicators { get; set; } = new();
    public List<DoubleHighEvidenceDto> Evidences { get; set; } = new();
    public List<DoubleHighReportDto> RecentReports { get; set; } = new();

    /// <summary>项目依据文件（政策文件等）。</summary>
    public List<DoubleHighProjectDocumentDto> Documents { get; set; } = new();
}

/// <summary>项目依据文件（政策文件等）。</summary>
public class DoubleHighProjectDocumentDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }

    /// <summary>显示名称。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>说明（出处 / 文号）。</summary>
    public string? Description { get; set; }

    /// <summary>原始文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>文件大小（字节）。</summary>
    public long FileSize { get; set; }

    /// <summary>公开访问地址（本地存储时为空，需走下载接口）。</summary>
    public string? FileUrl { get; set; }

    public int SortOrder { get; set; }
}

public class CreateUpdateDoubleHighIndicatorDto
{
    public Guid? ParentId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string IndicatorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public DoubleHighDataSourceType DataSourceType { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// 填报类型。为 null 时保留原有设置（兼容旧版调用方），新建指标时按数据来源推导。
    /// </summary>
    public DoubleHighIndicatorType? IndicatorType { get; set; }

    /// <summary>评分规则。为 null 时保留原有设置（兼容旧版调用方）。</summary>
    public DoubleHighScoringRule? ScoringRule { get; set; }

    /// <summary>必备佐证清单。为 null 时保留原有设置（兼容旧版调用方）。</summary>
    public List<string>? RequiredEvidences { get; set; }

    /// <summary>设置依据。</summary>
    public string? Basis { get; set; }

    /// <summary>引用的项目依据文件 id（为空 = 手写依据文本）。</summary>
    public Guid? BasisDocumentId { get; set; }

    /// <summary>数据来源的文字描述。</summary>
    public string? DataSourceName { get; set; }

    /// <summary>取值范围类型（仅系统取数指标）：为空表示「随项目对象」。</summary>
    public DoubleHighObjectType? ScopeType { get; set; }

    /// <summary>取值范围 id（资源库 / 专业 / 课程）。</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>取值范围名称。</summary>
    public string? ScopeName { get; set; }
}

public class CreateUpdateDoubleHighProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>评估模板 Key。</summary>
    public string? TemplateKey { get; set; }

    /// <summary>评估对象。</summary>
    public string? AssessmentTarget { get; set; }

    /// <summary>填报人。</summary>
    public string? ReporterName { get; set; }

    /// <summary>评估对象类型（教学资源库 / 专业 / 课程 / 手动填写）。</summary>
    public DoubleHighObjectType ObjectType { get; set; }

    /// <summary>评估对象 id（资源库 / 专业 / 课程）。</summary>
    public Guid? ObjectId { get; set; }

    /// <summary>评估对象名称（手动填写时为老师输入的名字）。</summary>
    public string? ObjectName { get; set; }

    public List<CreateUpdateDoubleHighIndicatorDto> Indicators { get; set; } = new();
}

/// <summary>按模板创建评估项目（自动预置指标、权重与评分规则）。</summary>
public class CreateDoubleHighProjectFromTemplateDto
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssessmentTarget { get; set; }
    public string? ReporterName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>评估对象类型（教学资源库 / 专业 / 课程 / 手动填写）。</summary>
    public DoubleHighObjectType ObjectType { get; set; }

    /// <summary>评估对象 id（资源库 / 专业 / 课程）。</summary>
    public Guid? ObjectId { get; set; }

    /// <summary>评估对象名称（手动填写时为老师输入的名字）。</summary>
    public string? ObjectName { get; set; }
}

public class CreateDoubleHighEvidenceDto
{
    public Guid ProjectId { get; set; }
    public Guid IndicatorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighEvidenceType EvidenceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? ExternalLink { get; set; }
    public int SortOrder { get; set; }

    /// <summary>佐证名称（对应必备佐证清单中的某一项）。</summary>
    public string? EvidenceName { get; set; }

    /// <summary>上传文件的原始文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>上传文件大小（字节）。</summary>
    public long FileSize { get; set; }
}

/// <summary>教师填报内容（数值 / 说明 / 自评备注）。</summary>
public class SaveDoubleHighReportingDto
{
    public Guid IndicatorId { get; set; }
    public decimal? Value { get; set; }
    public string? TextValue { get; set; }
    public string? Note { get; set; }
}

/// <summary>评估模板（新建项目时预置指标）。</summary>
public class DoubleHighTemplateIndicatorDto
{
    public string Name { get; set; } = string.Empty;
    public DoubleHighIndicatorType IndicatorType { get; set; }
    public string? Unit { get; set; }
    public DoubleHighDataSourceType DataSourceType { get; set; }
    public string? DataSourceName { get; set; }
    public decimal Weight { get; set; }
    public DoubleHighScoringRule ScoringRule { get; set; }
    public decimal? TargetValue { get; set; }
    public List<string> RequiredEvidences { get; set; } = new();
    public string? Basis { get; set; }
}

public class DoubleHighTemplateDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>模板适用对象（课程 / 专业 / 团队）。</summary>
    public string ScopeLabel { get; set; } = string.Empty;

    public string? Description { get; set; }
    public List<DoubleHighTemplateIndicatorDto> Indicators { get; set; } = new();
}

/// <summary>评估对象候选项（教学资源库 / 专业 / 课程），用于新建项目选对象、逐指标确认取值范围。</summary>
public class DoubleHighObjectOptionDto
{
    public Guid Id { get; set; }

    /// <summary>对象名称（课程为课程标题）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>补充说明（如资源库的主建专业、课程所属专业）。</summary>
    public string? Extra { get; set; }
}

public class SaveDoubleHighIndicatorValueDto
{
    public Guid IndicatorId { get; set; }
    public decimal Value { get; set; }
    public string? Note { get; set; }
}

public class PagedDoubleHighProjectRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public DoubleHighProjectStatus? Status { get; set; }
}

public class GetDoubleHighReportsInput : PagedAndSortedResultRequestDto
{
    public Guid? ProjectId { get; set; }
}
