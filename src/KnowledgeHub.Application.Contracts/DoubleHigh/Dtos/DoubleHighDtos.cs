using System;
using System.Collections.Generic;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.DoubleHigh.Dtos;

public class DoubleHighIndicatorValueSnapshotDto
{
    public Guid IndicatorId { get; set; }
    public decimal Value { get; set; }
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
    public int EvidenceCount { get; set; }
    public decimal CompletionRate { get; set; }
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
}

public class DoubleHighProjectDetailDto : DoubleHighProjectDto
{
    public DoubleHighDashboardDto Dashboard { get; set; } = new();
    public List<DoubleHighIndicatorDto> Indicators { get; set; } = new();
    public List<DoubleHighEvidenceDto> Evidences { get; set; } = new();
    public List<DoubleHighReportDto> RecentReports { get; set; } = new();
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
}

public class CreateUpdateDoubleHighProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DoubleHighProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<CreateUpdateDoubleHighIndicatorDto> Indicators { get; set; } = new();
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
